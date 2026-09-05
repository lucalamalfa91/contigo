using Contigo.Quotes.Application.Assessment;
using Contigo.Quotes.Application.Strategy;
using Contigo.Quotes.Domain;
using Contigo.SharedKernel;

namespace Contigo.Quotes.Tests;

/// <summary>
/// Proves task E05/F03/US01/T01's execution step: <see cref="NegotiationStrategyCalculator"/>
/// computes a deterministic opening target/acceptable range/walk-away threshold plus the seven
/// canonical levers with rationale — parent story us-01-negotiation-strategy AC-1 — with no
/// database, no HTTP call and no LLM call anywhere in the path. Mirrors
/// <c>Contigo.Quotes.Tests.TargetSavingCalculatorTests</c>'s own shape/style (this module's other
/// "pure calculator, honest abstain, determinism" test).
/// </summary>
public sealed class NegotiationStrategyCalculatorTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    // Far from any calendar quarter-end (Mar 31/Jun 30/Sep 30/Dec 31), so the QuarterEnd lever's
    // "no immediate pressure" branch is the one under test unless a case says otherwise.
    private static readonly DateOnly MidQuarterDate = new(2026, 2, 10);

    private static QuoteLine Line(
        decimal? quantity = 100m,
        string? unit = "seats",
        string? term = "12 months",
        int? normalizedTermMonths = 12,
        decimal? unitPrice = 2300m) =>
        new()
        {
            TenantId = TenantId.New(),
            QuoteId = EntityId.New(),
            Description = "Sales Cloud Enterprise",
            Quantity = quantity,
            Unit = unit,
            Term = term,
            NormalizedTermMonths = normalizedTermMonths,
            UnitPrice = unitPrice,
            CreatedAt = CreatedAt,
        };

    private static LineTargetSaving TargetSaving(decimal? low = 1500m, decimal? high = 1800m) =>
        new(low, high, null, null, null, null, "test-fixture target-saving");

    // ----- AC-1: opening target / acceptable range / walk-away threshold -----

    [Fact]
    public void Opening_target_steps_one_range_width_below_the_low_end()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: 2300m), totalLineCountOnQuote: 1, TargetSaving(1500m, 1800m), MidQuarterDate);

        // range width = 1800 - 1500 = 300; opening = 1500 - 300 = 1200
        Assert.Equal(1200m, result.OpeningTarget);
        Assert.Equal(1500m, result.AcceptableRangeLow);
        Assert.Equal(1800m, result.AcceptableRangeHigh);
    }

    [Fact]
    public void Opening_target_never_goes_negative()
    {
        // range width = 1800 - 100 = 1700; naive opening = 100 - 1700 = -1600, clamped to 0.
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: 2300m), totalLineCountOnQuote: 1, TargetSaving(100m, 1800m), MidQuarterDate);

        Assert.Equal(0m, result.OpeningTarget);
    }

    [Fact]
    public void Walk_away_threshold_steps_one_range_width_above_the_high_end()
    {
        // Reproduces spec §12.1's own illustrative example: range [410,440] -> walk-away 470
        // (440 + (440-410) = 470), current price (520) never binds the clamp.
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: 520_000m), totalLineCountOnQuote: 1, TargetSaving(410_000m, 440_000m), MidQuarterDate);

        Assert.Equal(470_000m, result.WalkAwayThreshold);
    }

    [Fact]
    public void Walk_away_threshold_never_exceeds_the_current_unit_price()
    {
        // Naive walk-away = 1800 + 300 = 2100, but the current price is only 1900.
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: 1900m), totalLineCountOnQuote: 1, TargetSaving(1500m, 1800m), MidQuarterDate);

        Assert.Equal(1900m, result.WalkAwayThreshold);
    }

    [Fact]
    public void Zero_width_range_still_produces_a_coherent_strategy()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: 1500m), totalLineCountOnQuote: 1, TargetSaving(1500m, 1500m), MidQuarterDate);

        Assert.Equal(1500m, result.OpeningTarget);
        Assert.Equal(1500m, result.WalkAwayThreshold);
    }

    [Fact]
    public void Explanation_names_the_target_range_and_walk_away_and_cites_the_rules()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: 2300m), totalLineCountOnQuote: 1, TargetSaving(1500m, 1800m), MidQuarterDate);

        Assert.Contains("1200", result.Explanation);
        Assert.Contains("1500", result.Explanation);
        Assert.Contains("1800", result.Explanation);
        Assert.Contains("2100", result.Explanation);
        Assert.Contains("Appendix C rule 6", result.Explanation);
        Assert.Contains("§12.1", result.Explanation);
    }

    // ----- AC-1: levers, always all seven, in spec §12.1's own order -----

    [Fact]
    public void Always_returns_exactly_the_seven_canonical_levers_in_spec_order()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        Assert.Equal(
            [
                NegotiationLeverType.Volume,
                NegotiationLeverType.Term,
                NegotiationLeverType.Utilization,
                NegotiationLeverType.Alternatives,
                NegotiationLeverType.QuarterEnd,
                NegotiationLeverType.Bundle,
                NegotiationLeverType.PaymentTerms,
            ],
            result.Levers.Select(l => l.LeverType));
    }

    [Fact]
    public void Volume_lever_cites_the_line_quantity_and_unit_when_recorded()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(quantity: 250m, unit: "licenses"), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        var volume = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Volume);
        Assert.Contains("250", volume.Rationale);
        Assert.Contains("licenses", volume.Rationale);
    }

    [Fact]
    public void Volume_lever_is_honest_when_no_quantity_is_recorded()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(quantity: null), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        var volume = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Volume);
        Assert.Contains("No quantity is recorded", volume.Rationale);
    }

    [Fact]
    public void Term_lever_cites_the_recorded_term_and_normalized_months()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(term: "36 months", normalizedTermMonths: 36), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        var term = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Term);
        Assert.Contains("36 months", term.Rationale);
    }

    [Fact]
    public void Term_lever_is_honest_when_no_term_is_recorded()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(term: null, normalizedTermMonths: null), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        var term = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Term);
        Assert.Contains("No commitment term is recorded", term.Rationale);
    }

    [Fact]
    public void Bundle_lever_cites_the_sibling_line_count_when_more_than_one_line_is_on_the_quote()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 4, TargetSaving(), MidQuarterDate);

        var bundle = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Bundle);
        Assert.Contains("4", bundle.Rationale);
    }

    [Fact]
    public void Bundle_lever_is_honest_when_this_is_the_only_line_on_the_quote()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        var bundle = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Bundle);
        Assert.Contains("No other line items are bundled", bundle.Rationale);
    }

    [Theory]
    [InlineData("2026-12-20", true)]   // 11 days before Dec 31
    [InlineData("2027-01-03", true)]   // 3 days after Dec 31 (previous year) — wraps correctly
    [InlineData("2026-02-10", false)]  // 41 days from the previous Dec 31, 49 from the next Mar 31
    public void QuarterEnd_lever_reflects_proximity_to_a_calendar_quarter_end(string asOfDateText, bool expectPressure)
    {
        var asOfDate = DateOnly.Parse(asOfDateText);

        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 1, TargetSaving(), asOfDate);

        var quarterEnd = Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.QuarterEnd);
        Assert.Equal(expectPressure, quarterEnd.Rationale.Contains("is within", StringComparison.Ordinal));
    }

    [Fact]
    public void Utilization_alternatives_and_payment_terms_levers_are_always_generic_today()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        Assert.Contains(
            "No usage/utilization data",
            Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Utilization).Rationale);
        Assert.Contains(
            "No alternative-supplier quote",
            Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.Alternatives).Rationale);
        Assert.Contains(
            "No payment-term data",
            Assert.Single(result.Levers, l => l.LeverType == NegotiationLeverType.PaymentTerms).Rationale);
    }

    // ----- Honest abstain: no usable target range / no current price (Appendix C rule 10) -----

    [Fact]
    public void Abstains_with_no_levers_when_target_saving_itself_is_null()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 1, targetSaving: null, MidQuarterDate);

        Assert.Null(result.OpeningTarget);
        Assert.Null(result.AcceptableRangeLow);
        Assert.Null(result.AcceptableRangeHigh);
        Assert.Null(result.WalkAwayThreshold);
        Assert.Empty(result.Levers);
        Assert.Contains("Appendix C rule 10", result.Explanation);
    }

    [Fact]
    public void Abstains_when_target_saving_has_no_recommended_range()
    {
        // The shape TargetSavingCalculator itself returns for insufficient benchmark data: every
        // numeric field null, only Explanation populated.
        var insufficientData = new LineTargetSaving(null, null, null, null, null, null, "insufficient data");

        var result = NegotiationStrategyCalculator.Compute(
            Line(), totalLineCountOnQuote: 1, insufficientData, MidQuarterDate);

        Assert.Null(result.OpeningTarget);
        Assert.Empty(result.Levers);
        Assert.Contains("insufficient data", result.Explanation);
    }

    [Fact]
    public void Abstains_when_the_line_has_no_current_unit_price()
    {
        var result = NegotiationStrategyCalculator.Compute(
            Line(unitPrice: null), totalLineCountOnQuote: 1, TargetSaving(), MidQuarterDate);

        Assert.Null(result.OpeningTarget);
        Assert.Null(result.WalkAwayThreshold);
        Assert.Empty(result.Levers);
        Assert.Contains("UnitPrice is not recorded", result.Explanation);
    }

    [Fact]
    public void Rejects_a_null_line_argument()
    {
        Assert.Throws<ArgumentNullException>(
            () => NegotiationStrategyCalculator.Compute(null!, 1, TargetSaving(), MidQuarterDate));
    }

    // ----- Determinism (Appendix C rule 6) -----

    [Fact]
    public void Same_inputs_produce_the_same_result_every_time()
    {
        var line = Line();
        var targetSaving = TargetSaving();

        var first = NegotiationStrategyCalculator.Compute(line, 2, targetSaving, MidQuarterDate);
        var second = NegotiationStrategyCalculator.Compute(line, 2, targetSaving, MidQuarterDate);

        Assert.Equal(first.OpeningTarget, second.OpeningTarget);
        Assert.Equal(first.AcceptableRangeLow, second.AcceptableRangeLow);
        Assert.Equal(first.AcceptableRangeHigh, second.AcceptableRangeHigh);
        Assert.Equal(first.WalkAwayThreshold, second.WalkAwayThreshold);
        Assert.Equal(first.Explanation, second.Explanation);
        // Compared as sequences (not via the outer record's own Equals): IReadOnlyList<T> has no
        // structural equality of its own, so two independently-built lists of equal, record-typed
        // NegotiationLever elements compare correctly only when asserted directly like this.
        Assert.Equal(first.Levers, second.Levers);
    }
}
