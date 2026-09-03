using Contigo.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Audit.Infrastructure;

/// <summary>
/// The read side of the Audit module (task E01/F06/US02/T02, the `audit-query` artifact; story
/// us-02-audit-baseline AC-2: "GET /api/audit returns authorized, tenant-scoped events"). Mirrors
/// <see cref="AuditWriter"/>'s belt-and-suspenders tenancy model (ADR-009): the ambient
/// `app.tenant_id` claim (<see cref="Contigo.SharedKernel.Tenancy.TenantRlsConnectionInterceptor"/>)
/// already restricts every row Postgres RLS will hand back on this connection to one tenant, and
/// the explicit <c>Where(TenantId == tenantId)</c> in <see cref="AuditQueryService"/> is the
/// application-level filter ADR-009 asks for on top of that RLS backstop — never removed even
/// though RLS alone would already narrow the result set.
/// </summary>
public interface IAuditQueryService
{
    /// <summary>
    /// The most recent audit events for <paramref name="tenantId"/>, newest first (see
    /// <c>AuditEventConfiguration</c>'s <c>(tenant_id, occurred_at)</c> index, shaped for exactly
    /// this read). Deliberately capped at <see cref="AuditQueryService.MaxResults"/> — V1 exposes no
    /// caller-supplied paging (the product spec's own API table lists no query parameters for this
    /// route); a follow-up task adds cursor/skip paging if a tenant's audit trail outgrows one
    /// bounded read.
    /// </summary>
    Task<IReadOnlyList<AuditEventRecord>> GetEventsAsync(
        TenantId tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// One audit event as returned to a caller of <see cref="IAuditQueryService"/> — the query side's
/// own read model, deliberately independent of <see cref="AuditEntry"/> (the *write*-side contract
/// every module already depends on; see <c>Contigo.Audit.Domain.AuditEvent</c>'s own doc comment on
/// why the two are allowed to diverge). Omits the tenant id: every record in one response already
/// belongs to the one tenant the caller was authorized for, so repeating it on every row would be
/// redundant. Uses a plain <see cref="Guid"/> rather than <c>Contigo.Audit.Domain.AuditEvent</c>'s
/// own <c>EntityId</c> wrapper because this record is a public HTTP JSON contract, and
/// <c>EntityId</c> has no custom JSON converter registered anywhere in this solution — serializing
/// the wrapper directly would leak it as a nested <c>{"value":"..."}</c> object instead of a plain
/// GUID string.
/// </summary>
public sealed record AuditEventRecord(
    Guid Id,
    string Actor,
    string Action,
    string ResourceType,
    string ResourceId,
    DateTimeOffset OccurredAt,
    string? Detail);

/// <summary>EF Core-backed <see cref="IAuditQueryService"/> (see the interface's own doc comment).</summary>
public sealed class AuditQueryService(AuditDbContext dbContext) : IAuditQueryService
{
    /// <summary>See <see cref="IAuditQueryService.GetEventsAsync"/> for why this is not (yet) caller-configurable.</summary>
    internal const int MaxResults = 200;

    public async Task<IReadOnlyList<AuditEventRecord>> GetEventsAsync(
        TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AuditEvents
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(MaxResults)
            .Select(e => new AuditEventRecord(
                e.Id.Value, e.Actor, e.Action, e.ResourceType, e.ResourceId, e.OccurredAt, e.Detail))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
