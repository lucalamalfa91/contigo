using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// A flagged risk, optionally traced back to the clause it was derived from. Confidence
/// travels with the fact, never presented as certainty the extraction didn't earn
/// (Appendix C rule 2; rule 10 — return uncertainty instead of fabricated precision).
/// </summary>
public sealed class Risk : TenantScopedEntity
{
    public required EntityId ContractId { get; set; }
    public EntityId? ClauseId { get; set; }

    public required string RiskType { get; set; }
    public required RiskSeverity Severity { get; set; }
    public required string Description { get; set; }
    public double? Confidence { get; set; }
    public string? Status { get; set; }

    public required DateTimeOffset IdentifiedAt { get; set; }
}
