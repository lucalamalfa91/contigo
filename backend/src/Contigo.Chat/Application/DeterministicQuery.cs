using Contigo.SharedKernel;

namespace Contigo.Chat.Application;

/// <summary>
/// The deterministic query plan <see cref="DeterministicQueryPlanner"/> derives from a
/// <see cref="QueryRouteDecision"/> whose <see cref="Domain.QueryIntent"/> is
/// <see cref="Domain.QueryIntent.Structured"/>. A closed set of three cases (one per
/// <see cref="Domain.DeterministicQueryKind"/> member) so <see cref="DeterministicQueryHandler"/>
/// can pattern-match exhaustively — see that type's <c>Handle</c> method.
/// </summary>
/// <param name="Question">The routed question this plan answers, unchanged from
/// <see cref="QueryRouteDecision.Question"/>.</param>
public abstract record DeterministicQuery(string Question)
{
    /// <summary>"Which contracts renew/expire in the next N days/months/years?"</summary>
    /// <param name="Days">The window size in days — months/years are already normalized to days
    /// by <see cref="DeterministicQueryPlanner"/>, so <see cref="DeterministicQueryHandler"/>
    /// never has to know the original unit.</param>
    public sealed record RenewalWindow(string Question, int Days) : DeterministicQuery(Question);

    /// <summary>"What is our annual spend [with a given supplier]?"</summary>
    /// <param name="SupplierId">Null when no supplier filter could be resolved — see
    /// <see cref="DeterministicQueryPlanner"/>'s doc comment for why free text alone cannot
    /// resolve one today.</param>
    public sealed record AnnualSpend(string Question, EntityId? SupplierId) : DeterministicQuery(Question);

    /// <summary>A <see cref="Domain.QueryIntent.Structured"/> question this task does not cover
    /// yet — see <see cref="Domain.DeterministicQueryKind.Unsupported"/> for why this case
    /// exists.</summary>
    /// <param name="Reason">Caller-facing explanation of the gap (never a fabricated
    /// answer — Appendix C rule 10).</param>
    public sealed record Unsupported(string Question, string Reason) : DeterministicQuery(Question);
}
