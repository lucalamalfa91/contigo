namespace Contigo.Quotes.Application.Strategy;

/// <summary>
/// One recommended negotiation lever plus its rationale (task E05/F03/US01/T01,
/// negotiation-strategy; parent story us-01-negotiation-strategy AC-1's own "levers with
/// rationale"). <see cref="NegotiationStrategyCalculator"/> always returns exactly the seven
/// <see cref="NegotiationLeverType"/> values, in the fixed order spec §12.1's own example lists
/// them — never a variable-length subset — so a caller always sees the full canonical playbook,
/// with <see cref="Rationale"/> distinguishing a lever this quote's own data actually grounds from
/// one that is still a generic, always-available negotiation tactic (see that type's own doc
/// comment for why omitting an ungrounded lever, rather than saying so honestly, would be a worse
/// default).
///
/// <para>
/// <b>Deliberately does not carry a structured evidence/citation field yet</b>: parent story AC-2
/// ("Rationale cites explicit evidence per lever", Appendix C rule 2) is task E05/F03/US01/T02's
/// own, separate scope (strategy-evidence) — this task's own coding objective is "levers with
/// rationale", not "levers with evidence citations". Adding an evidence shape here now would
/// pre-empt that task's own design decision (e.g. whether it reuses
/// <c>Contigo.AiGateway.Contracts.AiCitation</c>'s shape or a Quotes-local record) without that
/// task's own context. <see cref="Rationale"/> already names the concrete quote/line fact behind a
/// grounded lever inline (e.g. the actual quantity, term or bundle count) — task-02's job is to
/// make that pointer structured and queryable, not to invent the underlying fact.
/// </para>
/// </summary>
/// <param name="LeverType">Which of the seven canonical levers this is.</param>
/// <param name="Rationale">Human-readable, deterministic explanation of why this lever applies —
/// see <see cref="NegotiationStrategyCalculator"/>'s own doc comment for the deterministic-vs-LLM
/// split (AC-3; Appendix C rule 6) this text currently sits on.</param>
public sealed record NegotiationLever(NegotiationLeverType LeverType, string Rationale);
