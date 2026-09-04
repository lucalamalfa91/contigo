namespace Contigo.AiGateway.Contracts;

/// <summary>
/// Input to the `answer` role: a question plus the evidence the caller has already retrieved and
/// authorized (ADR-011 "authorization before retrieval"). An empty <see cref="Evidence"/> list is
/// valid input, not an error — it is how a caller represents "authorized retrieval found
/// nothing", which the gateway must answer with "cannot determine" rather than guessing (spec
/// §8.4; Appendix C rule 10).
/// </summary>
/// <param name="Question">The user's question, already scoped by the caller's own intent routing (spec §8.3).</param>
/// <param name="Evidence">Pre-retrieved, pre-authorized evidence to ground the answer in.</param>
public sealed record AiAnswerRequest(
    string Question,
    IReadOnlyList<AiEvidenceSnippet> Evidence);
