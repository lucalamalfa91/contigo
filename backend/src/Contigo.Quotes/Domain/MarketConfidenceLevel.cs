namespace Contigo.Quotes.Domain;

/// <summary>
/// A qualitative confidence tier for a market assessment's benchmark provenance — the product
/// spec's own UI convention of showing "Benchmark confidence: High / Medium / Low" (spec §11.2
/// "Assessment output" table's own "Benchmark confidence | High / Medium / Low" row) rather than a
/// bare decimal a user cannot interpret unaided. Computed deterministically from
/// <c>Contigo.Benchmark.Contracts.BenchmarkResult.Confidence</c> by
/// <c>Contigo.Quotes.Application.Assessment.MarketAssessmentProvenanceClassifier</c> — see that
/// type's own doc comments for the exact thresholds. Mirrors
/// <c>Contigo.Savings.Domain.SavingsConfidenceLevel</c> exactly (same enum shape, same thresholds,
/// deliberately duplicated rather than shared: ADR-002 forbids <c>Contigo.Quotes</c> from
/// referencing <c>Contigo.Savings</c> — its own allowed Contigo references are exactly
/// <c>[SharedKernel, Benchmark]</c> — the same "each module owns its own copy of a small,
/// module-local classification" pattern <c>SavingsConfidenceLevel</c>'s own doc comment already
/// documents for <c>Contigo.Renewals.Domain.ContractRiskLevel</c>).
/// </summary>
public enum MarketConfidenceLevel
{
    Low,
    Medium,
    High,
}
