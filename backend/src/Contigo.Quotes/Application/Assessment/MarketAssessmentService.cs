using Contigo.Benchmark;
using Contigo.Quotes.Domain;
using Contigo.Quotes.Infrastructure;
using Contigo.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Quotes.Application.Assessment;

/// <summary>
/// Implements task E05/F02/US01/T01 (market-assessment; parent story us-01-market-assessment AC-1
/// "Match normalized line items to the Benchmark Service (multi-dimensional)", AC-2's "flag" half,
/// AC-3 "<c>GET /api/quotes/{id}/assessment</c> returns the assessment with
/// confidence/provenance") and task E05/F02/US01/T02 (target-saving; AC-2's "recommended target
/// range + potential saving" half — <see cref="LineMarketAssessment.TargetSaving"/> is computed from
/// the same <c>line.Quantity</c> this service now also passes through per line below). The one place
/// in <c>Contigo.Quotes</c> that actually calls
/// <see cref="IBenchmarkService"/> — <c>Contigo.Quotes.Infrastructure.ServiceCollectionExtensions
/// .AddQuotesModule</c>'s own doc comment named this task as the "later step" that would need to
/// (it deliberately left <c>AddBenchmarkModule()</c> uncalled until a real caller existed). Calling
/// the interface here is exactly what Appendix C's benchmark rule sanctions — "Never call a
/// benchmark provider **directly**" names the provider adapter, not this abstraction; see
/// <see cref="IBenchmarkService"/>'s own doc comment: "Domain modules depend on this abstraction
/// only." Mirrors <c>Contigo.IntegrationTests.R3EndToEndTests</c>'s own convention of resolving
/// <see cref="IBenchmarkService"/> directly where a real caller is needed, rather than inventing an
/// extra indirection layer.
///
/// Read-only and computed fresh on every call — nothing here is persisted (unlike
/// <c>Contigo.Quotes.Application.Normalization.SkuNormalizationService</c>'s own
/// <c>QuoteLine.MatchStatus</c> column): no ADR/spec names an "Assessment" table shape beyond the
/// module-map's one-line mention, and a live benchmark distribution can change between two calls —
/// caching or persisting a market position that can silently go stale would be a worse default than
/// recomputing it, the same "computed fresh, not stored" posture
/// <c>Contigo.Savings.Application.PriceComparisonResult.Provenance</c> already takes for its own
/// derived property.
/// </summary>
public sealed class MarketAssessmentService(QuotesDbContext dbContext, IBenchmarkService benchmarkService)
{
    /// <summary>
    /// Assesses every <see cref="QuoteLine"/> currently persisted for <paramref name="quoteId"/>/
    /// <paramref name="tenantId"/>. One <see cref="IBenchmarkService.GetBenchmarkAsync"/> call per
    /// line that has enough data to build a query (see <see cref="MarketAssessmentQueryBuilder"/>) —
    /// never batched/cached across lines, since two lines can legitimately name different
    /// products/SKUs that must each be matched independently (spec §10.4).
    /// </summary>
    public async Task<Result<QuoteMarketAssessment>> AssessAsync(
        TenantId tenantId, EntityId quoteId, CancellationToken cancellationToken = default)
    {
        var quote = await dbContext.Quotes
            .SingleOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quoteId, cancellationToken)
            .ConfigureAwait(false);

        if (quote is null)
        {
            return Result<QuoteMarketAssessment>.Failure($"Quote {quoteId} was not found for this tenant.");
        }

        var lines = await dbContext.QuoteLines
            .Where(l => l.TenantId == tenantId && l.QuoteId == quoteId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var assessments = new List<LineMarketAssessment>(lines.Count);

        foreach (var line in lines)
        {
            assessments.Add(await AssessLineAsync(line, quote, cancellationToken).ConfigureAwait(false));
        }

        return Result<QuoteMarketAssessment>.Success(new QuoteMarketAssessment(quoteId, assessments));
    }

    private async Task<LineMarketAssessment> AssessLineAsync(
        QuoteLine line, Quote quote, CancellationToken cancellationToken)
    {
        var queryResult = MarketAssessmentQueryBuilder.Build(quote, line);

        if (queryResult.IsFailure)
        {
            return new LineMarketAssessment(
                line.Id,
                MarketAssessmentStatus.QuoteDataUnresolved,
                Position: null,
                UnitPrice: line.UnitPrice,
                Quantity: line.Quantity,
                Benchmark: null,
                Explanation: queryResult.Error);
        }

        var benchmarkOutcome = await benchmarkService
            .GetBenchmarkAsync(queryResult.Value, cancellationToken)
            .ConfigureAwait(false);

        if (benchmarkOutcome.IsFailure)
        {
            // The Benchmark Service itself failed (a provider-adapter-level problem, not a data
            // gap this module can name more precisely) — reported the same honest way a missing
            // query dimension is, never silently swallowed into a misleading "assessed" state.
            return new LineMarketAssessment(
                line.Id,
                MarketAssessmentStatus.QuoteDataUnresolved,
                Position: null,
                UnitPrice: line.UnitPrice,
                Quantity: line.Quantity,
                Benchmark: null,
                Explanation: benchmarkOutcome.Error);
        }

        var benchmark = benchmarkOutcome.Value;
        var classification = MarketAssessmentCalculator.Classify(line.UnitPrice!.Value, benchmark);

        return new LineMarketAssessment(
            line.Id,
            classification.Status,
            classification.Position,
            line.UnitPrice,
            line.Quantity,
            benchmark,
            classification.Explanation);
    }
}
