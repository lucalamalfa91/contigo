using Contigo.Savings.Domain;
using Contigo.Savings.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Savings.Application;

/// <summary>
/// Implements task E04/F02/US02/T01 (savings-opportunity): `GET /api/savings` (list) and `PATCH
/// /api/savings/{id}` (update status/owner) — parent story us-02-savings-opportunity AC-1/AC-2.
/// Also exposes <see cref="CreateAsync"/> ("identify"), not yet wired to an HTTP route — see
/// <see cref="CreateSavingsOpportunityRequest"/>'s own doc comment for why.
///
/// Same shape as <c>Contigo.Renewals.Application.RenewalActionService</c>: owns its own tenant scope
/// (<see cref="ITenantContext.BeginScope"/>) rather than trusting one is already active (nothing
/// upstream opens one — see <c>Contigo.Api.Program</c>), validates every field before writing
/// anything, and writes one append-only <see cref="IAuditWriter"/> entry per successful mutation
/// (spec §14.1 "Comprehensive audit logging for access and data changes"; Appendix C rule 9).
/// <see cref="IAuditWriter"/> lives in <c>Contigo.SharedKernel</c>, not <c>Contigo.Audit</c>, so
/// depending on it does not cross the ADR-002 module boundary (`Contigo.Savings`'s allow-list is
/// `[SharedKernel, Benchmark]`) — the same trick <c>RenewalActionService</c> already uses.
/// </summary>
public sealed class SavingsOpportunityService(
    SavingsDbContext dbContext, ITenantContext tenantContext, IClock clock, IAuditWriter auditWriter)
{
    public const string TypeRequiredError = "'type' is required.";
    public const string CurrentSpendMustBePositiveError = "'currentSpend' must be a positive amount.";
    public const string CurrencyRequiredError = "'currency' is required.";
    public const string EstimatedSavingsRangeInvalidError =
        "'estimatedSavingsLow' and 'estimatedSavingsHigh' must both be >= 0, with low <= high.";
    public const string ConfidenceOutOfRangeError = "'confidence' must be between 0 and 1 inclusive.";
    public const string OwnerCannotBeBlankError = "'owner' cannot be blank when provided.";
    public const string NoFieldsToUpdateError = "At least one of 'owner' or 'status' must be provided.";

    /// <summary>Returned by <see cref="UpdateAsync"/> when no opportunity with the given id exists
    /// for the caller's tenant. <c>Contigo.Api.SavingsEndpointExtensions</c> maps exactly this string
    /// to 404; every other failure maps to 400 — same convention
    /// <c>Contigo.Documents.Contracts.Application.ContractCorrectionService.ContractNotFoundError</c>
    /// already establishes.</summary>
    public const string NotFoundError = "Savings opportunity not found.";

    public static string StatusInvalidError { get; } =
        $"'status' must be one of: {string.Join(", ", Enum.GetNames<SavingsOpportunityStatus>())}.";

    /// <summary><see cref="AuditEntry.Action"/> for a successful <see cref="CreateAsync"/> call —
    /// past-tense, matching this codebase's established convention (see
    /// <c>Contigo.Renewals.Application.RenewalActionService</c>'s own doc comment for the full
    /// list).</summary>
    private const string AuditIdentifiedAction = "savings_opportunity.identified";

    /// <summary><see cref="AuditEntry.Action"/> for a successful <see cref="UpdateAsync"/> call.</summary>
    private const string AuditUpdatedAction = "savings_opportunity.updated";

    private const string AuditResourceType = "savings_opportunity";

    /// <summary>Same interim-actor placeholder as
    /// <c>Contigo.Renewals.Application.RenewalActionService.UnattributedActor</c> — see that type's
    /// own doc comment for why: ADR-010 (Entra ID/OIDC) is not wired into this host yet, so there is
    /// no validated caller identity to record.</summary>
    private const string UnattributedActor = "unattributed";

    /// <summary>
    /// "Identify" a new opportunity — validates every field, then persists it with
    /// <see cref="SavingsOpportunityStatus.Identified"/> and no <see cref="SavingsOpportunity.Owner"/>.
    /// See <see cref="CreateSavingsOpportunityRequest"/>'s own doc comment for why no HTTP route
    /// calls this yet.
    /// </summary>
    public async Task<Result<SavingsOpportunityResult>> CreateAsync(
        TenantId tenantId,
        CreateSavingsOpportunityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return Result<SavingsOpportunityResult>.Failure(TypeRequiredError);
        }

        if (request.CurrentSpend <= 0m)
        {
            return Result<SavingsOpportunityResult>.Failure(CurrentSpendMustBePositiveError);
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return Result<SavingsOpportunityResult>.Failure(CurrencyRequiredError);
        }

        if (request.EstimatedSavingsLow < 0m
            || request.EstimatedSavingsHigh < 0m
            || request.EstimatedSavingsHigh < request.EstimatedSavingsLow)
        {
            return Result<SavingsOpportunityResult>.Failure(EstimatedSavingsRangeInvalidError);
        }

        if (request.Confidence is < 0d or > 1d)
        {
            return Result<SavingsOpportunityResult>.Failure(ConfidenceOutOfRangeError);
        }

        using var _ = tenantContext.BeginScope(tenantId);

        var now = clock.UtcNow;
        var opportunity = new SavingsOpportunity
        {
            TenantId = tenantId,
            SupplierId = request.SupplierId,
            ContractId = request.ContractId,
            Type = request.Type,
            CurrentSpend = request.CurrentSpend,
            Currency = request.Currency,
            EstimatedSavingsLow = request.EstimatedSavingsLow,
            EstimatedSavingsHigh = request.EstimatedSavingsHigh,
            Confidence = request.Confidence,
            Status = SavingsOpportunityStatus.Identified,
            Owner = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.SavingsOpportunities.Add(opportunity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Recorded only once the write itself is durable, still inside this call's own tenant scope
        // (same placement as ContractCorrectionService.CorrectAsync's own "write then audit entry").
        await auditWriter.WriteAsync(
            new AuditEntry(
                tenantId,
                UnattributedActor,
                AuditIdentifiedAction,
                AuditResourceType,
                opportunity.Id.Value.ToString(),
                now,
                $"type={opportunity.Type} currentSpend={opportunity.CurrentSpend} {opportunity.Currency}"),
            cancellationToken).ConfigureAwait(false);

        return Result<SavingsOpportunityResult>.Success(ToResult(opportunity));
    }

    /// <summary>Backs `GET /api/savings` — every opportunity for the caller's tenant, newest
    /// identified first. No filters yet (status/supplier/etc.) — a follow-up, the same
    /// minimal-first shape several other list endpoints in this codebase started with.</summary>
    public async Task<IReadOnlyList<SavingsOpportunityResult>> ListAsync(
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = tenantContext.BeginScope(tenantId);

        var rows = await dbContext.SavingsOpportunities
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToResult).ToList();
    }

    /// <summary>
    /// Backs `PATCH /api/savings/{id}` — updates whichever of <paramref name="owner"/>/
    /// <paramref name="status"/> the caller supplied (non-null) on the one opportunity matching
    /// (<paramref name="tenantId"/>, <paramref name="id"/>). Validation runs before any query or
    /// write, so an invalid request leaves the database untouched (same "phase 1: validate
    /// everything, phase 2: mutate" discipline
    /// <c>ContractCorrectionService.CorrectAsync</c> already follows). Setting
    /// <paramref name="status"/> to <see cref="SavingsOpportunityStatus.Realized"/> only changes
    /// this column — see that enum member's own doc comment for the audit-tracked realized-value
    /// gap this leaves for task E04/F02/US02/T02.
    /// </summary>
    public async Task<Result<SavingsOpportunityResult>> UpdateAsync(
        TenantId tenantId,
        EntityId id,
        string? owner,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (owner is null && status is null)
        {
            return Result<SavingsOpportunityResult>.Failure(NoFieldsToUpdateError);
        }

        if (owner is not null && string.IsNullOrWhiteSpace(owner))
        {
            return Result<SavingsOpportunityResult>.Failure(OwnerCannotBeBlankError);
        }

        SavingsOpportunityStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<SavingsOpportunityStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Result<SavingsOpportunityResult>.Failure(StatusInvalidError);
            }

            parsedStatus = candidate;
        }

        using var _ = tenantContext.BeginScope(tenantId);

        var existing = await dbContext.SavingsOpportunities
            .SingleOrDefaultAsync(o => o.TenantId == tenantId && o.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return Result<SavingsOpportunityResult>.Failure(NotFoundError);
        }

        var now = clock.UtcNow;

        if (owner is not null)
        {
            existing.Owner = owner;
        }

        if (parsedStatus is { } newStatus)
        {
            existing.Status = newStatus;
        }

        existing.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditWriter.WriteAsync(
            new AuditEntry(
                tenantId,
                UnattributedActor,
                AuditUpdatedAction,
                AuditResourceType,
                existing.Id.Value.ToString(),
                now,
                $"owner={existing.Owner} status={existing.Status}"),
            cancellationToken).ConfigureAwait(false);

        return Result<SavingsOpportunityResult>.Success(ToResult(existing));
    }

    private static SavingsOpportunityResult ToResult(SavingsOpportunity opportunity) => new(
        opportunity.Id,
        opportunity.SupplierId,
        opportunity.ContractId,
        opportunity.Type,
        opportunity.CurrentSpend,
        opportunity.Currency,
        opportunity.EstimatedSavingsLow,
        opportunity.EstimatedSavingsHigh,
        opportunity.Confidence,
        opportunity.Status,
        opportunity.Owner,
        opportunity.CreatedAt,
        opportunity.UpdatedAt);
}
