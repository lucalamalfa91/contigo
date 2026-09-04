using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// A flagged risk, optionally traced back to the clause it was derived from. Confidence
/// travels with the fact, never presented as certainty the extraction didn't earn
/// (Appendix C rule 2; rule 10 — return uncertainty instead of fabricated precision).
/// <see cref="ClauseId"/> is nullable (a risk can be identified without a pre-extracted
/// clause backing it), so <see cref="SourceDocumentId"/>/<see cref="SourceSpan"/>/
/// <see cref="SourcePage"/> give every risk its own direct evidence pointer — never only an
/// indirect one through an optional clause.
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

    /// <summary>Direct evidence pointer (Appendix C rule 2), independent of <see cref="ClauseId"/>.</summary>
    public EntityId? SourceDocumentId { get; set; }
    public string? SourceSpan { get; set; }
    public int? SourcePage { get; set; }

    public required DateTimeOffset IdentifiedAt { get; set; }

    /// <summary>Optimistic-concurrency guard — see <see cref="Contract.Version"/>.</summary>
    public int Version { get; set; } = 1;
}
