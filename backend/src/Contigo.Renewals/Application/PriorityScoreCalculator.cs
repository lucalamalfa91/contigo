using System.Globalization;
using Contigo.Renewals.Domain;

namespace Contigo.Renewals.Application;

/// <summary>
/// The explainable renewal-priority calculator (task E03/F01/US02/T01, the wave-spec's
/// <c>renewal-priority</c> artifact; parent story us-02-priority-score, which depends on this
/// module's own <c>renewal-engine</c> artifact — <see cref="RenewalEngine"/> — for "days until
/// renewal"). Pure and synchronous, the same determinism convention as
/// <see cref="RenewalEngine"/> (Appendix C rule 6): the same <c>RenewalCalculationResult</c> plus
/// the same <see cref="RenewalPriorityInputs"/> always produce the same
/// <see cref="PriorityScoreResult"/> — no database call, no HTTP call, no LLM call anywhere in
/// <see cref="Calculate"/>.
///
/// <para>
/// Product spec §9.2: <c>"Priority Score = Spend Weight + Time Urgency + Benchmark Opportunity +
/// Price Increase Risk + Contract Risk"</c> (parent story AC-1 names "Price Increase Risk" as
/// "uplift risk" — both name the same spec §9.3 "Annual uplift" fact). Spec §9.2 also requires:
/// "Store both total score and component scores so the recommendation is explainable and tunable"
/// — AC-2, made concrete by <see cref="PriorityScoreComponent"/> being its own named field per
/// component rather than a single opaque number, and every branch below carrying a human-readable
/// <see cref="PriorityScoreComponent.Explanation"/>.
/// </para>
///
/// <para>
/// Every component is scored 0-<see cref="MaxComponentScore"/>;
/// <see cref="PriorityScoreResult.TotalScore"/> is their sum, 0-<see cref="MaxTotalScore"/>. A
/// component whose raw input is unknown never fabricates a guess (Appendix C rule 10): it
/// defaults to the documented no-data value — the conservative minimum (0) for
/// spend/time-urgency/price-increase-risk/contract-risk (an unknown fact must not inflate
/// priority), or the explicit midpoint <see cref="NeutralComponentScore"/> for benchmark
/// opportunity specifically, because parent story AC-3 names that exact rule ("reads the R3
/// benchmark only when available (else neutral)") — "neutral" is deliberately different from
/// "minimum": an unknown market position is not evidence of a bad deal (which would argue for the
/// minimum) any more than it is evidence of a great one (which would argue for the maximum). Every
/// threshold below is this task's first-pass, documented default — parent story task-02
/// (priority-explainability) is where they become tunable configuration, not a re-derivation of
/// the formula itself.
/// </para>
/// </summary>
public sealed class PriorityScoreCalculator
{
    /// <summary>Maximum score any single <see cref="PriorityScoreComponent"/> can contribute.</summary>
    public const decimal MaxComponentScore = 20m;

    /// <summary>Maximum possible <see cref="PriorityScoreResult.TotalScore"/> — five components at
    /// <see cref="MaxComponentScore"/> each (product spec §9.2's five named terms).</summary>
    public const decimal MaxTotalScore = MaxComponentScore * 5m;

    /// <summary>The "no data" value for the benchmark-opportunity component only (parent story
    /// AC-3) — the midpoint of 0-<see cref="MaxComponentScore"/>, deliberately distinct from every
    /// other component's no-data value (the minimum, 0 — see this class's own doc comment for
    /// why).</summary>
    public const decimal NeutralComponentScore = MaxComponentScore / 2m;

    /// <summary>
    /// Computes <paramref name="inputs"/>'s five priority-score components and their total for the
    /// contract <paramref name="renewal"/> already describes (relative to
    /// <see cref="RenewalCalculationResult.DaysUntilRenewal"/> — no second "days until renewal"
    /// computation happens here; see this class's own doc comment for why).
    /// </summary>
    public PriorityScoreResult Calculate(RenewalCalculationResult renewal, RenewalPriorityInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(renewal);
        ArgumentNullException.ThrowIfNull(inputs);

        var spendWeight = ComputeSpendWeight(inputs.AnnualSpend);
        var timeUrgency = ComputeTimeUrgency(renewal.DaysUntilRenewal);
        var benchmarkOpportunity = ComputeBenchmarkOpportunity(inputs.BenchmarkMarketPositionPercent);
        var priceIncreaseRisk = ComputePriceIncreaseRisk(inputs.AnnualUpliftPercent);
        var contractRisk = ComputeContractRisk(inputs.ContractRisk);

        var total = spendWeight.Score + timeUrgency.Score + benchmarkOpportunity.Score +
            priceIncreaseRisk.Score + contractRisk.Score;

        return new PriorityScoreResult(
            renewal.ContractId,
            total,
            spendWeight,
            timeUrgency,
            benchmarkOpportunity,
            priceIncreaseRisk,
            contractRisk);
    }

    /// <summary>Convenience batch form of <see cref="Calculate"/>, mirroring
    /// <see cref="RenewalEngine.CalculateMany"/> for product spec §9.1's "daily scheduler for each
    /// active contract" shape — one result per <paramref name="contracts"/> entry, in the same
    /// order, no aggregation.</summary>
    public IReadOnlyList<PriorityScoreResult> CalculateMany(
        IEnumerable<(RenewalCalculationResult Renewal, RenewalPriorityInputs Inputs)> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);

        return contracts.Select(c => Calculate(c.Renewal, c.Inputs)).ToList();
    }

    private static PriorityScoreComponent ComputeSpendWeight(decimal? annualSpend)
    {
        if (annualSpend is null)
        {
            return new PriorityScoreComponent(0m,
                "AnnualSpend is unknown: spend weight defaults to the minimum (0) rather than " +
                "guessing (Appendix C rule 10).");
        }

        var spend = annualSpend.Value;
        if (spend <= 0m)
        {
            return new PriorityScoreComponent(0m,
                $"AnnualSpend ({Fmt(spend)}) is not a positive amount: spend weight defaults to " +
                "the minimum (0).");
        }

        return spend switch
        {
            >= 500_000m => new PriorityScoreComponent(20m,
                $"AnnualSpend ({Fmt(spend)}) is 500,000 or more: maximum spend weight (20)."),
            >= 250_000m => new PriorityScoreComponent(16m,
                $"AnnualSpend ({Fmt(spend)}) is 250,000 or more: spend weight 16."),
            >= 100_000m => new PriorityScoreComponent(12m,
                $"AnnualSpend ({Fmt(spend)}) is 100,000 or more: spend weight 12."),
            >= 25_000m => new PriorityScoreComponent(8m,
                $"AnnualSpend ({Fmt(spend)}) is 25,000 or more: spend weight 8."),
            _ => new PriorityScoreComponent(4m,
                $"AnnualSpend ({Fmt(spend)}) is below 25,000: spend weight 4."),
        };
    }

    private static PriorityScoreComponent ComputeTimeUrgency(int? daysUntilRenewal)
    {
        if (daysUntilRenewal is null)
        {
            return new PriorityScoreComponent(0m,
                "No determined renewal date (RenewalCalculationResult.DaysUntilRenewal is null): " +
                "time urgency defaults to the minimum (0) rather than guessing (Appendix C rule 10).");
        }

        var days = daysUntilRenewal.Value;

        // Tiered at product spec §9.1's own default renewal threshold windows
        // (365/270/180/120/90/60/30 days) so this component's explanation lines up with the same
        // windows the cancellation-alerts threshold-scheduler (task E03/F02/US01/T01) alerts on.
        return days switch
        {
            < 0 => new PriorityScoreComponent(20m,
                $"Renewal is {-days} day(s) overdue: maximum time urgency (20)."),
            <= 30 => new PriorityScoreComponent(20m,
                $"{days} day(s) until renewal, within the 30-day window: maximum time urgency (20)."),
            <= 60 => new PriorityScoreComponent(17m,
                $"{days} day(s) until renewal, within the 60-day window: time urgency 17."),
            <= 90 => new PriorityScoreComponent(15m,
                $"{days} day(s) until renewal, within the 90-day window: time urgency 15."),
            <= 120 => new PriorityScoreComponent(12m,
                $"{days} day(s) until renewal, within the 120-day window: time urgency 12."),
            <= 180 => new PriorityScoreComponent(9m,
                $"{days} day(s) until renewal, within the 180-day window: time urgency 9."),
            <= 270 => new PriorityScoreComponent(6m,
                $"{days} day(s) until renewal, within the 270-day window: time urgency 6."),
            <= 365 => new PriorityScoreComponent(3m,
                $"{days} day(s) until renewal, within the 365-day window: time urgency 3."),
            _ => new PriorityScoreComponent(0m,
                $"{days} day(s) until renewal, beyond the 365-day window: minimum time urgency (0)."),
        };
    }

    private static PriorityScoreComponent ComputeBenchmarkOpportunity(decimal? marketPositionPercent)
    {
        if (marketPositionPercent is null)
        {
            return new PriorityScoreComponent(NeutralComponentScore,
                "R3 benchmark data is not available for this contract (Contigo.Benchmark." +
                "IBenchmarkService has no query operations yet): benchmark opportunity defaults " +
                $"to neutral ({Fmt(NeutralComponentScore)}) rather than assuming an opportunity " +
                "or its absence (parent story AC-3).");
        }

        var position = marketPositionPercent.Value;

        // >= ladder: "position" is how far ABOVE market the contract's spend sits (spec §9.3's own
        // "18% above benchmark" example) — the higher above market, the bigger the opportunity to
        // negotiate a saving. A position at or below -20% (well below market) is the smallest
        // opportunity, not a data gap, so it still lands at the minimum rather than neutral.
        return position switch
        {
            >= 20m => new PriorityScoreComponent(20m,
                $"Spend is {Fmt(position)}% above the R3 benchmark market rate: maximum benchmark " +
                "opportunity (20)."),
            >= 5m => new PriorityScoreComponent(15m,
                $"Spend is {Fmt(position)}% above the R3 benchmark market rate: benchmark " +
                "opportunity 15."),
            >= -5m => new PriorityScoreComponent(NeutralComponentScore,
                $"Spend is within 5% of the R3 benchmark market rate ({Fmt(position)}%): neutral " +
                $"benchmark opportunity ({Fmt(NeutralComponentScore)})."),
            >= -20m => new PriorityScoreComponent(5m,
                $"Spend is {Fmt(-position)}% below the R3 benchmark market rate: benchmark " +
                "opportunity 5."),
            _ => new PriorityScoreComponent(0m,
                $"Spend is {Fmt(-position)}% below the R3 benchmark market rate: minimum " +
                "benchmark opportunity (0)."),
        };
    }

    private static PriorityScoreComponent ComputePriceIncreaseRisk(decimal? annualUpliftPercent)
    {
        if (annualUpliftPercent is null)
        {
            return new PriorityScoreComponent(0m,
                "AnnualUpliftPercent is unknown: price-increase risk defaults to the minimum (0) " +
                "rather than guessing (Appendix C rule 10).");
        }

        var uplift = annualUpliftPercent.Value;
        if (uplift <= 0m)
        {
            return new PriorityScoreComponent(0m,
                $"AnnualUpliftPercent ({Fmt(uplift)}%) is zero or a decrease: no price-increase " +
                "risk (0).");
        }

        return uplift switch
        {
            >= 20m => new PriorityScoreComponent(20m,
                $"AnnualUpliftPercent ({Fmt(uplift)}%) is 20% or more: maximum price-increase " +
                "risk (20)."),
            >= 12m => new PriorityScoreComponent(18m,
                $"AnnualUpliftPercent ({Fmt(uplift)}%) is 12% or more: price-increase risk 18."),
            >= 7m => new PriorityScoreComponent(14m,
                $"AnnualUpliftPercent ({Fmt(uplift)}%) is 7% or more: price-increase risk 14."),
            >= 3m => new PriorityScoreComponent(9m,
                $"AnnualUpliftPercent ({Fmt(uplift)}%) is 3% or more: price-increase risk 9."),
            _ => new PriorityScoreComponent(4m,
                $"AnnualUpliftPercent ({Fmt(uplift)}%) is above 0% but below 3%: price-increase " +
                "risk 4."),
        };
    }

    private static PriorityScoreComponent ComputeContractRisk(ContractRiskLevel? riskLevel)
    {
        if (riskLevel is null)
        {
            return new PriorityScoreComponent(0m,
                "No ContractRiskLevel has been assessed for this contract: contract risk " +
                "defaults to the minimum (0) rather than assuming risk (Appendix C rule 10).");
        }

        return riskLevel.Value switch
        {
            ContractRiskLevel.Low => new PriorityScoreComponent(3m, "ContractRisk is Low: contract risk 3."),
            ContractRiskLevel.Medium => new PriorityScoreComponent(9m, "ContractRisk is Medium: contract risk 9."),
            ContractRiskLevel.High => new PriorityScoreComponent(15m, "ContractRisk is High: contract risk 15."),
            ContractRiskLevel.Critical => new PriorityScoreComponent(20m, "ContractRisk is Critical: maximum contract risk (20)."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(riskLevel), riskLevel.Value, "Unknown ContractRiskLevel."),
        };
    }

    /// <summary>Culture-invariant, unpadded decimal formatting for explanation strings — so
    /// <see cref="PriorityScoreComponent.Explanation"/> text (and any test asserting against it) is
    /// stable regardless of the running culture (no thousands separator or currency symbol, which
    /// this codebase does not model on <c>Contract.AnnualSpend</c> anywhere).</summary>
    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
