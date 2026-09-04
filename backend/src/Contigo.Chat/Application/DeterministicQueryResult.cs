using Contigo.Chat.Domain;
using Contigo.SharedKernel;

namespace Contigo.Chat.Application;

/// <summary>
/// The outcome of <see cref="DeterministicQueryHandler.Handle"/> — parent story
/// us-01-query-router AC-2 ("Structured questions hit deterministic queries/filters (no LLM)")
/// made concrete and testable: every field here is computed by a pure filter/aggregation over the
/// <see cref="ContractFact"/> list the caller supplied, never by a language model.
/// </summary>
/// <param name="Question">The question this result answers, unchanged from the routed decision.</param>
/// <param name="Kind">Which deterministic query family produced this result.</param>
/// <param name="MatchedContractIds">
/// The contracts the filter/aggregation matched — every contract summed into
/// <see cref="AggregateAnnualSpend"/> for <see cref="DeterministicQueryKind.AnnualSpend"/>, or
/// every contract renewing inside the window for <see cref="DeterministicQueryKind.RenewalWindow"/>.
/// Always empty for <see cref="DeterministicQueryKind.Unsupported"/>.
/// </param>
/// <param name="AggregateAnnualSpend">
/// The summed <c>Contract.AnnualSpend</c> for <see cref="DeterministicQueryKind.AnnualSpend"/>
/// (zero when zero contracts matched); null for every other <see cref="Kind"/> — "no aggregate
/// was computed" and "the matched contracts spent nothing" must stay distinguishable.
/// </param>
/// <param name="Explanation">Human-readable trace of what the handler did — not meant to be
/// shown to an end user as-is, but enough for a test (or a developer) to see *why* a result has
/// the shape it does without re-deriving it, the same role <see cref="QueryRouteDecision.Reason"/>
/// plays for routing.</param>
public sealed record DeterministicQueryResult(
    string Question,
    DeterministicQueryKind Kind,
    IReadOnlyList<EntityId> MatchedContractIds,
    decimal? AggregateAnnualSpend,
    string Explanation);
