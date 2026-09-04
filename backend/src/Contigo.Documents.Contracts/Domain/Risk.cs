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

    /// <summary>Page/section evidence pointer (Appendix C rule 2; spec §7.3). Added by task
    /// E02/F01/US02/T01 (us-02-staged-extraction, AC-2): the staged pipeline's own `risk` stage
    /// identifies a risk directly from source text, independently of whether it can also resolve
    /// a same-run <see cref="ClauseId"/> — <see cref="ClauseId"/> stays the *optional* derived
    /// trace-back link the type doc comment already describes, this is the fact's own evidence.
    /// </summary>
    public string? SourceSpan { get; set; }
    public int? SourcePage { get; set; }

    public required DateTimeOffset IdentifiedAt { get; set; }
}
