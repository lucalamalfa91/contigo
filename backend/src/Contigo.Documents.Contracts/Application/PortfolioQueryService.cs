using Contigo.Documents.Contracts.Domain;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Documents.Contracts.Application;

/// <summary>
/// Implements task E02/F03/US01/T01 (us-01-portfolio-list-filters, AC-1/AC-2/AC-3):
/// <c>GET /api/contracts</c> — the spec §8.1 portfolio columns (see <see cref="PortfolioListItem"/>),
/// with the server-side filters spec §8.1 lists (see <see cref="PortfolioFilter"/> for the one
/// filter deliberately not supported yet, and why), scoped to the caller's tenant (ADR-009).
///
/// Same shape as <see cref="DocumentQueryService"/>: nothing upstream (no middleware; see
/// <c>Contigo.Api.Program</c>) opens a tenant scope before a read runs, so this opens its own
/// <see cref="ITenantContext.BeginScope"/> rather than trusting one is already active. The
/// explicit <c>Where(TenantId == tenantId)</c> below is the application-level filter ADR-009
/// additionally asks for on top of the Postgres RLS backstop the `contract`/`risk` tables already
/// carry (added by the migration that produced the `contract-schema` artifact this task depends
/// on) — belt-and-suspenders, never removed even though RLS alone would already narrow the
/// result set.
/// </summary>
public sealed class PortfolioQueryService(DocumentsContractsDbContext dbContext, ITenantContext tenantContext)
{
    public async Task<IReadOnlyList<PortfolioListItem>> GetPortfolioAsync(
        TenantId tenantId, PortfolioFilter filter, CancellationToken cancellationToken = default)
    {
        // Entry point: open this call's own tenant scope (see the type doc comment). Must happen
        // before the query below, since the RLS connection interceptor reads
        // ITenantContext.Current only when the connection opens, which EF Core does lazily on
        // first use — i.e. inside this awaited call.
        using var _ = tenantContext.BeginScope(tenantId);

        var query = dbContext.Contracts.AsNoTracking().Where(c => c.TenantId == tenantId);

        // Nullable-to-nullable comparison (both sides EntityId?) so EF Core translates this
        // through the same NullableEntityIdConverter the column itself uses.
        if (filter.SupplierId is not null)
        {
            var supplierId = filter.SupplierId;
            query = query.Where(c => c.SupplierId == supplierId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        if (filter.AutoRenewal is { } autoRenewal)
        {
            query = query.Where(c => c.AutoRenewal == autoRenewal);
        }

        if (filter.MinAnnualSpend is { } minAnnualSpend)
        {
            query = query.Where(c => c.AnnualSpend != null && c.AnnualSpend >= minAnnualSpend);
        }

        if (filter.MaxAnnualSpend is { } maxAnnualSpend)
        {
            query = query.Where(c => c.AnnualSpend != null && c.AnnualSpend <= maxAnnualSpend);
        }

        // "Renewal period" only ever matches auto-renewing contracts — see PortfolioListItem's
        // own doc comment on why RenewalDate (the field this filters) is null otherwise.
        if (filter.RenewalFrom is { } renewalFrom)
        {
            query = query.Where(c => c.AutoRenewal && c.EndDate != null && c.EndDate >= renewalFrom);
        }

        if (filter.RenewalTo is { } renewalTo)
        {
            query = query.Where(c => c.AutoRenewal && c.EndDate != null && c.EndDate <= renewalTo);
        }

        var contracts = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Bulk-load risks for exactly the matched contracts (one extra query, not N+1) to compute
        // each row's "Risk" column as its highest-severity recorded risk. Grouping/Max happen
        // in-memory below (this list is already materialized), not translated to SQL, so
        // RiskSeverity's declared enum order (Low < Medium < High < Critical) — not its
        // `HasConversion<string>()` column representation — is what Max compares.
        var contractIds = contracts.Select(c => c.Id).ToList();
        var risksByContract = await dbContext.Risks
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && contractIds.Contains(r.ContractId))
            .Select(r => new { r.ContractId, r.Severity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var maxSeverityByContract = risksByContract
            .GroupBy(r => r.ContractId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.Severity));

        IEnumerable<PortfolioListItem> items = contracts.Select(c => new PortfolioListItem(
            c.Id.Value,
            c.SupplierId?.Value,
            c.Type,
            c.AnnualSpend,
            c.StartDate,
            c.EndDate,
            c.AutoRenewal ? c.EndDate : null,
            c.CancellationDeadline,
            c.AutoRenewal,
            c.Status,
            maxSeverityByContract.TryGetValue(c.Id, out var severity) ? severity : null));

        // Risk severity filters the *computed* column above, so — unlike every other filter —
        // it is applied in-memory after the join rather than translated to SQL against the
        // `risk` table directly.
        if (filter.Risk is { } risk)
        {
            items = items.Where(i => i.Risk == risk);
        }

        return items.ToList();
    }
}
