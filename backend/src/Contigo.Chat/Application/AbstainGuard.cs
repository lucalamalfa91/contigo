using Contigo.AiGateway.Contracts;

namespace Contigo.Chat.Application;

/// <summary>
/// No-fabrication guard for Ask Contigo grounded Q&amp;A (task E02/F04/US02/T02, abstain-guard;
/// parent story us-02-rag-citations AC-2/AC-3). <see cref="RagAnswerService"/> already forwards a
/// gateway result whose <see cref="AiAnswerResult.CanDetermine"/> is <see langword="false"/>
/// straight through as an honest "cannot determine" — <see cref="Fixtures.FixtureAiGateway
/// .AnswerAsync"/> already does exactly that for empty evidence (see that type's own doc comment).
/// This guard exists for the opposite, currently-unchecked case: a result that *claims*
/// <see cref="AiAnswerResult.CanDetermine"/> = <see langword="true"/> but whose
/// <see cref="AiAnswerResult.Citations"/> are not actually grounded in the caller-supplied,
/// already-authorized <see cref="AiEvidenceSnippet"/> evidence (Appendix C rule 2: "Never show a
/// consequential extracted fact without source evidence"; rule 10: "If data quality is insufficient,
/// return uncertainty instead of fabricated precision").
///
/// <para>
/// <see cref="Fixtures.FixtureAiGateway"/> cannot exercise the "intervenes" branch today — it only
/// ever echoes back citations built directly from the evidence it was handed, so it can never
/// fabricate one (see <c>FixtureAiGatewayAnswerTests</c>). This guard is here for the Foundry-backed
/// <see cref="Contigo.AiGateway.IAiGateway"/> implementation ADR-004 anticipates: a real model can
/// hallucinate a citation (cite a document it was never given), assert a claim backed by zero
/// citations, or return an empty answer while still claiming "determined" — nothing upstream of this
/// guard would catch any of those, because <see cref="RagAnswerService"/> has no database dependency
/// (ADR-011 auth-before-retrieval — see that type's own doc comment) and otherwise trusts the
/// gateway's own verdict verbatim.
/// </para>
///
/// <para>
/// Pure and synchronous — no I/O, no LLM call — so the same inputs always produce the same verdict
/// (Appendix C rule 6, the same determinism convention <see cref="AskContigoQueryRouter"/> and
/// <see cref="DeterministicQueryPlanner"/> already follow for their own decisions). Deliberately does
/// **not** re-retrieve or re-authorize anything itself: it only ever compares the gateway's own
/// output against the evidence list its caller already retrieved under ADR-011's auth-before
/// -retrieval rule, so this guard cannot become a second, competing retrieval path.
/// </para>
/// </summary>
public sealed class AbstainGuard
{
    /// <summary>
    /// Validates <paramref name="result"/> against <paramref name="evidence"/> and returns the
    /// outcome. When <paramref name="result"/> is already an honest abstention, or is a grounded
    /// answer whose every citation traces back to a supplied evidence document, it is returned
    /// unchanged (<see cref="AbstainGuardOutcome.Intervened"/> = <see langword="false"/>). Otherwise
    /// the guard forces a "cannot determine" result rather than let an unsupported or fabricated
    /// claim through, preserving <paramref name="result"/>'s own <see cref="AiAnswerResult.Metadata"/>
    /// (model id/version, prompt version, timestamp, input hash) so the call stays fully
    /// reproducible/auditable (ADR-011) even when the guard intervenes.
    /// </summary>
    /// <param name="result">The gateway's own `answer` role result
    /// (<see cref="Contigo.AiGateway.IAiGateway.AnswerAsync"/>).</param>
    /// <param name="evidence">The same already-retrieved, already-authorized evidence the gateway
    /// was given (<see cref="RagAnswerService.AnswerAsync"/>'s own <c>evidence</c> parameter) — the
    /// only source of truth a citation may point back to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> or
    /// <paramref name="evidence"/> is <see langword="null"/>.</exception>
    public AbstainGuardOutcome Enforce(AiAnswerResult result, IReadOnlyList<AiEvidenceSnippet> evidence)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(evidence);

        if (!result.CanDetermine)
        {
            // Already an honest "cannot determine" (FixtureAiGateway's empty-evidence path, or a
            // real model that abstained on its own) — nothing to guard.
            return new AbstainGuardOutcome(result, Intervened: false, Reason: null);
        }

        if (result.Citations.Count == 0)
        {
            return Abstained(
                result,
                "gateway claimed CanDetermine=true with zero citations — an unsupported claim " +
                "(Appendix C rule 2: never show a fact without source evidence).");
        }

        if (string.IsNullOrWhiteSpace(result.Answer))
        {
            return Abstained(
                result,
                "gateway claimed CanDetermine=true with no answer text — nothing to ground.");
        }

        var evidenceDocumentIds = evidence
            .Select(snippet => snippet.DocumentId)
            .ToHashSet(StringComparer.Ordinal);

        var ungroundedCitation = result.Citations
            .FirstOrDefault(citation => !evidenceDocumentIds.Contains(citation.DocumentId));

        if (ungroundedCitation is not null)
        {
            return Abstained(
                result,
                $"citation '{ungroundedCitation.DocumentId}' does not match any of the " +
                $"{evidence.Count} evidence document(s) handed to the gateway — a fabricated or " +
                "hallucinated citation (Appendix C rule 10: uncertainty over fabricated precision).");
        }

        return new AbstainGuardOutcome(result, Intervened: false, Reason: null);
    }

    private static AbstainGuardOutcome Abstained(AiAnswerResult original, string reason) =>
        new(
            new AiAnswerResult(CanDetermine: false, Answer: null, Citations: [], original.Metadata),
            Intervened: true,
            reason);
}

/// <summary>
/// <see cref="AbstainGuard.Enforce"/>'s outcome: the (possibly forced-abstain) result callers should
/// actually use, plus whether the guard intervened and why. <see cref="RagAnswerService"/> folds
/// <see cref="Intervened"/> into its own audit entry as a plain boolean — never <see cref="Reason"/>
/// itself, which exists for tests/diagnostics only: <see cref="Reason"/> is deliberately excluded
/// from the audit trail because it is free text describing the *gateway's* output, and ADR-011 caps
/// every AI-adjacent audit write to reproducibility fields (never raw prompt, retrieved content, or
/// model output) — the same "no content in logs" boundary
/// <c>Contigo.AiGateway.Logging.LoggingAiGateway</c> already draws for its own audit rows.
/// </summary>
/// <param name="Result">The result callers should actually use.</param>
/// <param name="Intervened">Whether <see cref="Result"/> was forced to abstain by this guard.</param>
/// <param name="Reason">Human-readable reason, present only when <see cref="Intervened"/> is
/// <see langword="true"/>.</param>
public sealed record AbstainGuardOutcome(AiAnswerResult Result, bool Intervened, string? Reason);
