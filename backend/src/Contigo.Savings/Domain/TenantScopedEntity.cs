using Contigo.SharedKernel;

namespace Contigo.Savings.Domain;

/// <summary>
/// Base type for every entity owned by the Savings bounded context (task E04/F02/US02/T01,
/// savings-opportunity — the first task to give <c>Contigo.Savings</c> a <c>DbContext</c>). ADR-009
/// requires every business table — including this module's own — to carry a not-null, indexed
/// <see cref="TenantId"/>, with Postgres Row-Level Security (wired by this module's own
/// `AddTenantRowLevelSecurity` migration) as the non-bypassable backstop. This is a module-local
/// copy of the same shape <c>Contigo.Audit.Domain.TenantScopedEntity</c>,
/// <c>Contigo.Documents.Contracts.Domain.TenantScopedEntity</c>,
/// <c>Contigo.Identity.Workspace.Domain.TenantScopedEntity</c> and
/// <c>Contigo.Renewals.Domain.TenantScopedEntity</c> already use: ADR-002's dependency-direction
/// rule (enforced by <c>Contigo.ArchitectureTests.DependencyDirectionTests</c>) only allows this
/// module to reference <c>Contigo.SharedKernel</c> (plus <c>Contigo.Benchmark</c>), never another
/// domain module's internals, so the base type is duplicated per module rather than shared.
/// </summary>
public abstract class TenantScopedEntity
{
    /// <summary>Client-generated primary key (ADR-003/ADR-009 do not require DB identity columns).</summary>
    public EntityId Id { get; set; } = EntityId.New();

    public required TenantId TenantId { get; set; }
}
