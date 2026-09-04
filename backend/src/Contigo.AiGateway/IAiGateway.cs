using Contigo.AiGateway.Contracts;
using Contigo.SharedKernel;

namespace Contigo.AiGateway;

/// <summary>
/// AI Gateway interface consumed by domain modules (Documents/Contracts, Chat). Domain modules
/// depend on this abstraction; the implementation behind it is the only place that touches
/// Foundry / Document Intelligence SDKs (module-map "AI Gateway" row;
/// us-01-ai-gateway-classification AC-3). ADR-004: each role is bound to a
/// configuration-selected model id — see
/// <see cref="Contigo.AiGateway.Configuration.AiGatewayModelOptions"/> — so a model swap is a
/// config change, never a code change.
///
/// Task E02/F01/US01/T01 implements four of ADR-004's five roles: <see cref="ClassifyAsync"/>,
/// <see cref="ExtractAsync"/>, <see cref="EmbedAsync"/>, <see cref="AnswerAsync"/> — exactly the
/// set named by this task's own "Coding objective". The fifth role, `ocr` (ADR-017), is
/// deliberately NOT added here: it belongs to task E02/F01/US02/T02 ("hybrid-ocr"), a later task
/// in the same wave whose own coding objective is "Add hybrid OCR pre-pass behind gateway" and
/// whose architecture decisions in force include ADR-017 (this task's do not — this task cites
/// only ADR-004 and ADR-011). Adding a fifth method now would pre-empt that task's own design of
/// the OCR request/result shape (full-document page map, ADR-017 "Implications for the
/// decomposition") for no benefit — nothing in this wave calls it yet.
///
/// Every method returns <see cref="Result{T}"/> (this codebase's convention for expected
/// failures — e.g. <c>Contigo.Documents.Contracts.Application.DocumentUploadService</c>) rather
/// than throwing, and every successful result carries an
/// <see cref="Contracts.AiCallMetadata"/> record (model id/version, prompt version, timestamp,
/// input hash) so callers — and task E02/F01/US01/T02's logging — always have brief §8's
/// reproducibility fields available without recomputing them.
/// </summary>
public interface IAiGateway
{
    /// <summary>
    /// `classify` role (ADR-004): recognizes the document type from its text
    /// (us-01-ai-gateway-classification AC-1/AC-2). See <see cref="AiDocumentType"/> for the
    /// fixed taxonomy.
    /// </summary>
    Task<Result<AiClassificationResult>> ClassifyAsync(
        AiClassificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// `extract` role (ADR-004): schema-constrained structured extraction for one bounded
    /// extraction stage (product spec §7.2/§7.3 — "avoid one giant prompt"). The gateway does not
    /// know or validate the domain schema — it returns whatever JSON the model produced against
    /// the caller-supplied <see cref="AiExtractionRequest.JsonSchema"/>, unparsed, so it stays
    /// reusable across every stage (metadata, commercial terms, dates, line items, clauses,
    /// obligations, risk) without a stage-specific overload.
    /// </summary>
    Task<Result<AiExtractionResult>> ExtractAsync(
        AiExtractionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// `embed` role (ADR-004): produces a fixed-dimension vector
    /// (<see cref="AiGatewayConstants.EmbeddingDimensions"/>) for pgvector semantic search / Ask
    /// Contigo RAG (spec §8.3).
    /// </summary>
    Task<Result<AiEmbeddingResult>> EmbedAsync(
        AiEmbeddingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// `answer` role (ADR-004): grounded Q&amp;A over caller-supplied, already-authorized
    /// evidence (ADR-011 "authorization before retrieval" — this gateway never retrieves; the
    /// caller resolves tenant/role/object authz and runs retrieval first). Returns citations or
    /// an explicit "cannot determine" (<see cref="AiAnswerResult.CanDetermine"/>) rather than
    /// fabricating (spec §8.4 "no evidence, no claim"; Appendix C rule 10).
    /// </summary>
    Task<Result<AiAnswerResult>> AnswerAsync(
        AiAnswerRequest request, CancellationToken cancellationToken = default);
}
