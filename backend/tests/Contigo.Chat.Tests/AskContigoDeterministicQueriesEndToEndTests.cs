using Contigo.Chat.Application;
using Contigo.Chat.Tests.TestSupport;
using Contigo.SharedKernel;

namespace Contigo.Chat.Tests;

/// <summary>
/// Task E02/F04/US01/T02's own end-to-end proof: product spec §8.3's two structured example
/// questions, taken all the way from raw question text (<see cref="AskContigoQueryRouter"/>,
/// task E02/F04/US01/T01) through planning (<see cref="DeterministicQueryPlanner"/>) to an
/// actual filtered/aggregated answer (<see cref="DeterministicQueryHandler"/>) — never touching
/// an LLM at any step (parent story us-01-query-router AC-2).
/// </summary>
public sealed class AskContigoDeterministicQueriesEndToEndTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 4);
    private static readonly FixedClock Clock = new(new DateTimeOffset(AsOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    private readonly AskContigoQueryRouter _router = new();
    private readonly DeterministicQueryPlanner _planner = new();
    private readonly DeterministicQueryHandler _handler = new(Clock);

    [Fact]
    public void Which_contracts_renew_in_the_next_120_days()
    {
        var renewingSoon = new ContractFact(EntityId.New(), null, 40_000m, AsOf.AddDays(90), AutoRenewal: true);
        var renewingLater = new ContractFact(EntityId.New(), null, 15_000m, AsOf.AddDays(300), AutoRenewal: true);
        var contracts = new[] { renewingSoon, renewingLater };

        var answer = AnswerAskContigo("Which contracts renew in the next 120 days?", contracts);

        Assert.Equal([renewingSoon.ContractId], answer.MatchedContractIds);
    }

    [Fact]
    public void What_is_our_Microsoft_annual_spend()
    {
        var msContract1 = new ContractFact(EntityId.New(), null, 640_000m, null, AutoRenewal: false);
        var msContract2 = new ContractFact(EntityId.New(), null, 60_000m, null, AutoRenewal: false);
        var contracts = new[] { msContract1, msContract2 };

        var answer = AnswerAskContigo("What is our Microsoft annual spend?", contracts);

        Assert.Equal(700_000m, answer.AggregateAnnualSpend);
    }

    private DeterministicQueryResult AnswerAskContigo(string question, IReadOnlyList<ContractFact> contracts)
    {
        var decision = _router.Route(question);
        Assert.True(decision.RequiresDeterministicQuery);

        var plan = _planner.Plan(decision);
        return _handler.Handle(plan, contracts);
    }
}
