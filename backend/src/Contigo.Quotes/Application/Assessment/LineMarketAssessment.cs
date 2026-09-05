using Contigo.Benchmark.Contracts;
using Contigo.Quotes.Domain;
using Contigo.SharedKernel;

namespace Contigo.Quotes.Application.Assessment;

/// <summary>
/// One <see cref="QuoteLine"/>'s market assessment — the per-line unit
/// <see cref="MarketAssessmentService.AssessAsync"/> assembles from
/// <see cref="MarketAssessmentQueryBuilder"/> + <c>Contigo.Benchmark.IBenchmarkService</c> +
/// <see cref="MarketAssessmentCalculator"/>. Parent story us-01-market-assessment AC-3 ("
/// <c>GET /api/quotes/{id}/assessment</c> returns the assessment with confidence/provenance") made
/// concrete and testable.
/// </summary>
/// <param name="QuoteLineId">Which line this assessment is for.</param>
/// <param name="Status">See <see cref="MarketAssessmentStatus"/>'s own doc comments.</param>
/// <param name="Position">The above/in-line/below flag — populated only when <paramref name="Status"/>
/// is <see cref="MarketAssessmentStatus.Assessed"/>.</param>
/// <param name="UnitPrice">The <see cref="QuoteLine.UnitPrice"/> this assessment compared — echoed
/// here so a caller never has to re-fetch the line to see what price a position/explanation refers
/// to. Populated whenever the line had one, independent of <paramref name="Status"/> (mirrors
/// <c>Contigo.Savings.Application.PriceComparisonResult.NormalizedUnitPrice</c>'s own "does not
/// depend on the benchmark" posture) — <see langword="null"/> only when the line itself had no
/// usable price (folded into <see cref="MarketAssessmentStatus.QuoteDataUnresolved"/>).</param>
/// <param name="Benchmark">The Benchmark Service result this assessment was computed from —
/// <see langword="null"/> exactly when <paramref name="Status"/> is
/// <see cref="MarketAssessmentStatus.QuoteDataUnresolved"/> (no query could be built, so no
/// benchmark call was ever made). Never re-fetched or recomputed by this record.</param>
/// <param name="Explanation">Human-readable trace of why this line has the status/position it
/// does.</param>
public sealed record LineMarketAssessment(
    EntityId QuoteLineId,
    MarketAssessmentStatus Status,
    MarketPosition? Position,
    decimal? UnitPrice,
    BenchmarkResult? Benchmark,
    string Explanation)
{
    /// <summary>
    /// Confidence + provenance "on the match" (AC-3) — <see cref="MarketAssessmentProvenanceClassifier.FromBenchmark"/>
    /// applied to <see cref="Benchmark"/>. Computed fresh on every access, not a stored field, the
    /// same "cannot drift from its one source of truth" shape
    /// <c>Contigo.Savings.Application.PriceComparisonResult.Provenance</c> already established.
    /// <see langword="null"/> exactly when <see cref="Benchmark"/> is <see langword="null"/> — spec
    /// §11.3's benchmark-trust rule ("never withhold provenance just because the comparison itself
    /// abstained") applies once a benchmark call was actually made
    /// (<see cref="MarketAssessmentStatus.InsufficientBenchmarkData"/> still carries one); it
    /// cannot apply to a line no query could even be built for.
    /// </summary>
    public MarketAssessmentProvenance? Provenance =>
        Benchmark is null ? null : MarketAssessmentProvenanceClassifier.FromBenchmark(Benchmark);
}
