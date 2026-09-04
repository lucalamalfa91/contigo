namespace Contigo.AiGateway.Contracts;

/// <summary>
/// Output of the `answer` role. <see cref="CanDetermine"/> is <see langword="false"/> whenever
/// the gateway abstains rather than fabricates (empty/insufficient evidence) — callers must check
/// it before showing <see cref="Answer"/>, which is <see langword="null"/> in that case (spec
/// §8.4 "no evidence, no claim"; Appendix C rule 10).
/// </summary>
/// <param name="CanDetermine">Whether a grounded answer could be produced from the given evidence.</param>
/// <param name="Answer">The grounded answer text, or <see langword="null"/> when <see cref="CanDetermine"/> is <see langword="false"/>.</param>
/// <param name="Citations">Source pointers backing <see cref="Answer"/> (spec §8.3/§8.4). Empty when <see cref="CanDetermine"/> is <see langword="false"/>.</param>
/// <param name="Metadata">Reproducibility metadata for this call (ADR-004, ADR-011).</param>
public sealed record AiAnswerResult(
    bool CanDetermine,
    string? Answer,
    IReadOnlyList<AiCitation> Citations,
    AiCallMetadata Metadata);
