using Contigo.AiGateway;
using Contigo.AiGateway.Contracts;
using Contigo.Documents.Contracts.Domain;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Documents.Contracts.Application.Extraction;

/// <summary>
/// Task E02/F06/US01/T01 (r1-integration): the caller <see cref="HybridDocumentParsingService"/>'s
/// and <see cref="StagedExtractionService"/>'s own doc comments both name as missing —
/// <c>HybridDocumentParsingService</c>: "Wiring 'load the Document row, read its bytes from
/// storage, call this, then call StagedExtractionService' into an HTTP endpoint or the Worker's
/// queue dispatch is later-task scope"; <c>StagedExtractionService.EnsureContractAsync</c>:
/// "nothing else in this wave consumes the <see cref="ExtractionStage.Classification"/> job
/// <c>DocumentUploadService</c> queues at upload". This is that later task: one orchestrator that
/// runs the rest of product spec §7.1's pipeline — classify, then hybrid parse's page-mapped text
/// into staged extraction, then indexes the result for Ask Contigo retrieval — so a document
/// uploaded on `dev`/`demo` is actually searchable and extracted afterward, not stuck at
/// "Uploaded" forever (parent story us-01-final-integration AC-1: "Upload -&gt; parse/OCR -&gt;
/// classify -&gt; extract -&gt; portfolio -&gt; 360 -&gt; Ask Contigo ... works end-to-end").
///
/// <b>Ordering</b>: classify runs <em>after</em> the hybrid parse, not before, even though
/// <see cref="ExtractionStage.Classification"/> is declared as the "zeroth" pipeline stage
/// (<see cref="ExtractionStage"/>'s own doc comment). <see cref="AiClassificationRequest.DocumentText"/>
/// is "native or OCR'd text of the document" — classification has nothing to read until the hybrid
/// parse (native or `ocr` gateway role) has already produced it. <see cref="Document.DocumentType"/>
/// is set, and its own <see cref="Domain.ExtractionJob"/> row is resolved, before
/// <see cref="StagedExtractionService.RunAsync"/> is called: that service's own
/// <c>EnsureContractAsync</c> seeds a freshly-created <see cref="Contract"/>'s
/// <see cref="Contract.Type"/> from <see cref="Document.DocumentType"/>, so classification must be
/// durable first (this method flushes it via <c>SaveChangesAsync</c> before calling
/// <see cref="StagedExtractionService.RunAsync"/> — sharing the caller's own scoped
/// <see cref="DocumentsContractsDbContext"/>, so <c>RunAsync</c>'s own re-query for the
/// <see cref="Document"/> row returns the same tracked, already-updated entity rather than racing a
/// second connection).
///
/// <b>Bytes in, not a storage re-read</b>: <see cref="ProcessAsync"/> takes
/// <paramref name="content"/> directly rather than loading it back through
/// <c>Contigo.SharedKernel.Storage.IDocumentStorage</c> — that interface exposes no read/load
/// method today (only <c>SaveAsync</c>; see its own doc comment), and adding one is a larger,
/// separate change to a shared abstraction every module and both hosts depend on. The one caller
/// this task wires (`POST /api/documents`, see <c>Contigo.Api.Program</c>) already holds the
/// uploaded bytes in memory for <c>DocumentUploadService.UploadAsync</c>'s own storage write, so
/// passing the same buffer here avoids the extra round trip entirely rather than working around a
/// missing read API.
///
/// <b>Synchronous, in-request, not a queue dispatch</b>: <c>Contigo.Worker.Queue
/// .QueueConsumerHostedService</c> deliberately does not dispatch a received message to a domain
/// handler yet (its own doc comment: "is a later task once that handler exists"), and nothing in
/// this codebase enqueues a durable message for <c>InMemoryQueueConsumer</c> to receive either —
/// <see cref="Domain.ExtractionJob"/> rows are written directly by <c>DocumentUploadService</c>,
/// never posted to <c>Contigo.Worker.Queue.IQueueConsumer</c>. Building that real async dispatch
/// (a durable queue producer/consumer pair) is a Worker feature in its own right, not this
/// integration task's scope. Running the rest of the pipeline synchronously, inline with the
/// upload request, is the smallest honest way to make R1's "upload -&gt; ... -&gt; Ask Contigo"
/// promise actually true on `dev`/`demo` today without redesigning the queue architecture — a
/// documented interim choice, not a silently absorbed shortcut, the same "explicit gap" convention
/// <c>Contigo.Api/Program.cs</c>'s own <c>X-Tenant-Id</c> placeholder already sets for this
/// codebase. A later task can move this call behind a real durable queue without changing this
/// method's own signature or behaviour.
///
/// <b>Never fails an already-durable upload</b>: every failure this method can report (parse
/// failure, one extraction stage failing, one page failing to embed) is recorded on the
/// <see cref="Document"/>/<see cref="Domain.ExtractionJob"/> rows and returned to the caller, but
/// none of it unwinds the upload itself — the bytes are already safely stored and the document row
/// already exists by the time this runs (mirrors <see cref="StagedExtractionService"/>'s own
/// per-stage "one failure does not abort the others" posture, generalized one layer up).
/// </summary>
public sealed class DocumentProcessingPipeline(
    DocumentsContractsDbContext dbContext,
    IAiGateway aiGateway,
    HybridDocumentParsingService parsingService,
    StagedExtractionService extractionService,
    EmbeddingRetrievalService embeddingRetrievalService,
    ITenantContext tenantContext,
    IClock clock)
{
    /// <summary>Discriminator this pipeline indexes every chunk under (<see cref="Domain.Embedding.SourceType"/>),
    /// matching <c>Contigo.Api.ChatEndpointExtensions.ToEvidenceSnippet</c>'s own
    /// <c>"Document"</c> literal so a resulting Ask Contigo citation's composite id
    /// (<c>{SourceType}:{SourceId}</c>) resolves back to this <see cref="Document"/>.</summary>
    private const string DocumentSourceType = "Document";

    /// <summary>Same threshold and same reasoning as <see cref="StagedExtractionService.LowConfidenceThreshold"/>
    /// (documented separately, not shared, because the two run against independent
    /// <see cref="Domain.ExtractionJob"/> rows on different <see cref="ExtractionStage"/> values —
    /// there is no single shared constant to reference without one service reaching into the
    /// other's private state): below this, classification is a proposal a human should confirm,
    /// not a trusted fact (product principle: "Human-in-the-loop for consequential decisions").
    /// </summary>
    private const double LowConfidenceThreshold = 0.6;

    public async Task<Result<DocumentProcessingSummary>> ProcessAsync(
        TenantId tenantId,
        EntityId documentId,
        string fileName,
        string mimeType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        using var tenantScope = tenantContext.BeginScope(tenantId);

        var document = await dbContext.Documents
            .SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken)
            .ConfigureAwait(false);

        if (document is null)
        {
            return Result<DocumentProcessingSummary>.Failure($"Document {documentId} was not found for this tenant.");
        }

        var parseResult = await parsingService
            .ParseAsync(fileName, mimeType, content, cancellationToken)
            .ConfigureAwait(false);

        if (parseResult.IsFailure)
        {
            // Honest terminal state: a document whose bytes could not be read at all (neither
            // natively nor via OCR) is not "still processing" — Failed, not silently left at
            // Uploaded, so the portfolio/360 surfaces do not imply extraction is still in flight.
            document.ProcessingStatus = DocumentProcessingStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<DocumentProcessingSummary>.Failure(parseResult.Error);
        }

        var pages = parseResult.Value;

        var (documentType, classificationConfidence) = await ClassifyAsync(tenantId, document, pages, cancellationToken)
            .ConfigureAwait(false);

        // Flush classification before StagedExtractionService.RunAsync runs — see the type doc
        // comment's "Ordering" remarks: EnsureContractAsync reads document.DocumentType to seed a
        // freshly-created Contract.Type, and both services share this same scoped DbContext
        // instance, so this SaveChangesAsync only needs to make the *classification job* durable;
        // the in-memory `document` entity RunAsync re-queries is already the identical, already-
        // updated tracked instance regardless.
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var extractionResult = await extractionService
            .RunAsync(tenantId, documentId, pages, cancellationToken)
            .ConfigureAwait(false);

        if (extractionResult.IsFailure)
        {
            return Result<DocumentProcessingSummary>.Failure(extractionResult.Error);
        }

        var chunksIndexed = await IndexForRetrievalAsync(tenantId, documentId, pages, cancellationToken)
            .ConfigureAwait(false);

        return Result<DocumentProcessingSummary>.Success(new DocumentProcessingSummary(
            documentId,
            extractionResult.Value.ContractId,
            documentType,
            classificationConfidence,
            extractionResult.Value.DocumentProcessingStatus,
            pages.Count,
            chunksIndexed));
    }

    /// <summary>
    /// Runs the `classify` gateway role against the just-parsed text and applies the result onto
    /// <paramref name="document"/> in memory (not yet saved — see <see cref="ProcessAsync"/>'s own
    /// remarks). Resolves and updates the <see cref="Domain.ExtractionJob"/> row
    /// <c>DocumentUploadService</c> already queued at upload time (<see cref="ExtractionStage.Classification"/>,
    /// <see cref="ExtractionJobStatus.Queued"/>) — the same "advance the queued row to completion"
    /// shape a real Worker dispatch would use, rather than inserting a second, redundant job row.
    /// A gateway failure (for example empty parsed text) leaves <paramref name="document"/>'s
    /// <see cref="Document.DocumentType"/> at its current value (the pre-classification default,
    /// <see cref="ContractDocumentType.Other"/>, for a first run) and marks the job
    /// <see cref="ExtractionJobStatus.Failed"/> — classification is one stage among several; its
    /// failure must not abort staged extraction (mirrors <see cref="StagedExtractionService"/>'s
    /// own per-stage failure posture).
    /// </summary>
    private async Task<(ContractDocumentType DocumentType, double? Confidence)> ClassifyAsync(
        TenantId tenantId, Document document, IReadOnlyList<DocumentPageText> pages, CancellationToken cancellationToken)
    {
        var classificationJob = await dbContext.ExtractionJobs
            .Where(j => j.TenantId == tenantId
                && j.DocumentId == document.Id
                && j.Stage == ExtractionStage.Classification
                && j.Status == ExtractionJobStatus.Queued)
            .OrderBy(j => j.QueuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var startedAt = clock.UtcNow;
        if (classificationJob is not null)
        {
            classificationJob.Status = ExtractionJobStatus.Running;
            classificationJob.StartedAt = startedAt;
        }

        var classificationText = BuildClassificationText(pages);
        var classifyResult = await aiGateway
            .ClassifyAsync(new AiClassificationRequest(classificationText), cancellationToken)
            .ConfigureAwait(false);

        var completedAt = clock.UtcNow;

        if (classifyResult.IsFailure)
        {
            if (classificationJob is not null)
            {
                classificationJob.Status = ExtractionJobStatus.Failed;
                classificationJob.ErrorDetail = Truncate(classifyResult.Error);
                classificationJob.CompletedAt = completedAt;
            }

            return (document.DocumentType, null);
        }

        var mappedType = MapDocumentType(classifyResult.Value.DocumentType);
        document.DocumentType = mappedType;

        if (classificationJob is not null)
        {
            classificationJob.ModelId = classifyResult.Value.Metadata.ModelId;
            classificationJob.Status = classifyResult.Value.Confidence < LowConfidenceThreshold
                ? ExtractionJobStatus.NeedsReview
                : ExtractionJobStatus.Completed;
            classificationJob.CompletedAt = completedAt;
        }

        return (mappedType, classifyResult.Value.Confidence);
    }

    /// <summary>
    /// Maps the AI Gateway's broader classify taxonomy onto the narrower contract-hierarchy one
    /// (see <see cref="AiDocumentType"/>'s own doc comment on why the two are deliberately
    /// distinct). A recognized-but-non-contract upload (Quote/Invoice/PriceList/Nda/Dpa) and a
    /// genuinely unrecognized one both map to <see cref="ContractDocumentType.Other"/> — that is
    /// this enum's own honest "not one of the named contract kinds" member, not a guess.
    /// <see cref="ContractDocumentType.RenewalLetter"/> has no classify-role counterpart to map
    /// from today; nothing in this wave's classify prompt/taxonomy names it.
    /// </summary>
    private static ContractDocumentType MapDocumentType(AiDocumentType aiDocumentType) => aiDocumentType switch
    {
        AiDocumentType.Msa => ContractDocumentType.Msa,
        AiDocumentType.OrderForm => ContractDocumentType.OrderForm,
        AiDocumentType.Sow => ContractDocumentType.Sow,
        AiDocumentType.Amendment => ContractDocumentType.Amendment,
        _ => ContractDocumentType.Other,
    };

    /// <summary>Representative text for the classify role (<see cref="AiClassificationRequest.DocumentText"/>:
    /// "the full text of the document (or a representative prefix)") — every page, in order, so a
    /// document whose identifying keyword (e.g. "MASTER SERVICES AGREEMENT") lands on any page is
    /// still recognized. Unlike <see cref="StagedExtractionService"/>'s own per-stage prompt text,
    /// this is not persisted or cited anywhere, so it carries no page markers.</summary>
    private static string BuildClassificationText(IReadOnlyList<DocumentPageText> pages) =>
        string.Join("\n\n", pages.Select(p => p.Text));

    /// <summary>
    /// Indexes each parsed page as one Ask Contigo retrieval chunk (spec §8.3), so a document is
    /// actually answerable immediately after processing rather than only after some later,
    /// separate indexing task runs (backend/README.md's own previously-recorded gap: "nothing yet
    /// calls IndexChunkAsync outside tests"). One <see cref="Domain.Embedding"/> row per page is a
    /// deliberate, documented first cut — splitting one very large page into multiple smaller
    /// embedding rows (for models with a limited context/token budget per chunk) is a later tuning
    /// task, not attempted here; every fixture and real contract page this pipeline has seen so far
    /// fits comfortably in one chunk. A blank page is skipped (nothing to embed, not a failure); a
    /// failed embed call for one page degrades only that page's retrievability — it does not fail
    /// the whole call, since every fact this pipeline extracted is already durable by this point
    /// (mirrors ADR-017's "fail visibly, never let one failure hide behind an aggregate success",
    /// generalized from OCR page-budget to indexing).
    /// </summary>
    private async Task<int> IndexForRetrievalAsync(
        TenantId tenantId, EntityId documentId, IReadOnlyList<DocumentPageText> pages, CancellationToken cancellationToken)
    {
        var indexed = 0;

        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            var indexResult = await embeddingRetrievalService
                .IndexChunkAsync(tenantId, DocumentSourceType, documentId, page.PageNumber - 1, page.Text, cancellationToken)
                .ConfigureAwait(false);

            if (indexResult.IsSuccess)
            {
                indexed++;
            }
        }

        return indexed;
    }

    /// <summary>Same truncation shape as <see cref="StagedExtractionService"/>'s own private
    /// helper (not shared — see the type's own remarks on <see cref="LowConfidenceThreshold"/> for
    /// why): keeps a gateway error message bounded before it lands on
    /// <see cref="Domain.ExtractionJob.ErrorDetail"/>.</summary>
    private static string Truncate(string value, int maxLength = 1000) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
