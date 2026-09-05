using System.Text;
using System.Text.Json;
using Contigo.AiGateway;
using Contigo.AiGateway.Contracts;
using Contigo.Documents.Contracts.Application.Extraction;
using Contigo.Quotes.Application.Extraction;
using Contigo.Quotes.Domain;
using Contigo.Quotes.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Api;

/// <summary>
/// Orchestrator for task E05/F01/US01/T01 (quote-extraction; parent story
/// us-01-quote-line-extraction AC-1/AC-2/AC-4). This is the one place in the solution that calls
/// both <c>Contigo.AiGateway</c> and <c>Contigo.Quotes</c>: ADR-002's dependency-direction rule
/// (<c>Contigo.ArchitectureTests.DependencyDirectionTests</c>) allows <c>Contigo.Quotes</c> to
/// reference only <c>Contigo.SharedKernel</c>/<c>Contigo.Benchmark</c>, and
/// <c>Contigo.Documents.Contracts</c> (whose <see cref="HybridDocumentParsingService"/> AC-4
/// reuses) cannot reference <c>Contigo.Quotes</c> either — only <c>Contigo.Api</c>, the
/// composition root, is allowed to see every module at once (backend/README.md's own "Dependency
/// direction" section). <c>internal</c>, not <c>public</c>: this is host-composition wiring, not a
/// domain module's own public API surface — enforced by
/// <c>Contigo.ArchitectureTests.DependencyDirectionTests.Host_must_not_contain_domain_types</c>,
/// the same treatment <c>Contigo.Worker.Queue.QueueConsumerHostedService</c> already gets for the
/// identical reason.
///
/// <b>AC-4</b> ("Scanned/image quote PDFs reuse the epic-02 hybrid OCR path (ADR-017); no 2-page
/// cap"): calls the exact same
/// <c>Contigo.Documents.Contracts.Application.Extraction.HybridDocumentParsingService</c> instance
/// task E02/F01/US02/T02 built for contracts — native text extraction when the file has
/// sufficient extractable text, the AI Gateway `ocr` role (Azure AI Document Intelligence) for
/// scanned/image/low-text quote PDFs, full document, no page cap. There is no quote-specific parse
/// code path to drift out of sync with the contract one.
///
/// <b>Synchronous, in-request, not a queue dispatch</b> — same deliberate interim choice as
/// <c>DocumentProcessingPipeline</c>'s own doc comment: nothing in this codebase dispatches a
/// queued job to a handler off a durable queue yet
/// (<c>Contigo.Worker.Queue.QueueConsumerHostedService</c>'s own doc comment), so running the
/// AI Gateway `extract` call inline, in the same `POST /api/quotes` request, is the smallest
/// honest way to make AC-1's "creates an extraction job" promise actually resolve to real line
/// items today.
///
/// <b>Never fails an already-durable upload</b>: any failure here (parse failure, gateway
/// failure, malformed payload) is recorded on the <see cref="Quote"/>/<see cref="QuoteExtractionJob"/>
/// rows and returned to the caller, but never unwinds the upload itself — the bytes are already
/// safely stored and the <see cref="Quote"/> row already exists by the time this runs (mirrors
/// <c>DocumentProcessingPipeline</c>'s identical posture).
/// </summary>
internal sealed class QuoteExtractionPipeline(
    QuotesDbContext dbContext,
    IAiGateway aiGateway,
    HybridDocumentParsingService parsingService,
    QuoteLineExtractionService lineExtractionService,
    ITenantContext tenantContext,
    IClock clock,
    IAuditWriter auditWriter)
{
    /// <summary>Caller-owned stage label for the AI Gateway's `extract` role (see
    /// <c>IAiGateway.ExtractAsync</c>'s own doc comment: "mirrors, but does not reference,
    /// ExtractionStage; the gateway must not depend on a domain module's enum").</summary>
    private const string StageName = "QuoteLineItems";

    /// <summary>Recorded actor for this pipeline's own audit entry — same "no human caller, label
    /// it as automation" convention as
    /// <c>StagedExtractionService.SystemActor</c>/<c>DocumentProcessingPipeline</c>'s own
    /// equivalents.</summary>
    private const string SystemActor = "system:quote-extraction";

    public async Task<Result<QuoteProcessingSummary>> ProcessAsync(
        TenantId tenantId,
        EntityId quoteId,
        string fileName,
        string mimeType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        using var tenantScope = tenantContext.BeginScope(tenantId);

        var quote = await dbContext.Quotes
            .SingleOrDefaultAsync(q => q.TenantId == tenantId && q.Id == quoteId, cancellationToken)
            .ConfigureAwait(false);

        if (quote is null)
        {
            return Result<QuoteProcessingSummary>.Failure($"Quote {quoteId} was not found for this tenant.");
        }

        // The row QuoteUploadService queued at upload — advanced to completion here, the same
        // "advance the queued row, never insert a second one" shape
        // DocumentProcessingPipeline.ClassifyAsync already uses for ExtractionJob.
        var job = await dbContext.QuoteExtractionJobs
            .Where(j => j.TenantId == tenantId
                && j.QuoteId == quoteId
                && j.Status == QuoteExtractionJobStatus.Queued)
            .OrderBy(j => j.QueuedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var startedAt = clock.UtcNow;
        if (job is not null)
        {
            job.Status = QuoteExtractionJobStatus.Running;
            job.StartedAt = startedAt;
        }

        var parseResult = await parsingService
            .ParseAsync(fileName, mimeType, content, cancellationToken)
            .ConfigureAwait(false);

        if (parseResult.IsFailure)
        {
            return await FailAsync(quote, job, parseResult.Error, cancellationToken).ConfigureAwait(false);
        }

        var pages = parseResult.Value;
        if (pages.Count == 0)
        {
            // ADR-017: "over-budget jobs fail visibly... they are not silently truncated" —
            // generalized here to "no readable text at all", mirroring
            // StagedExtractionService.RunAsync's identical guard.
            return await FailAsync(
                quote, job, "Quote extraction requires at least one page of document text.", cancellationToken)
                .ConfigureAwait(false);
        }

        var documentText = BuildPageMarkedText(pages);

        var extractRequest = new AiExtractionRequest(StageName, documentText, QuoteLineJsonSchema.LineItems());
        var extractResult = await aiGateway.ExtractAsync(extractRequest, cancellationToken).ConfigureAwait(false);

        if (extractResult.IsFailure)
        {
            return await FailAsync(quote, job, extractResult.Error, cancellationToken).ConfigureAwait(false);
        }

        if (job is not null)
        {
            job.ModelId = extractResult.Value.Metadata.ModelId;
        }

        QuoteLineExtractionOutcome outcome;
        var completedAt = clock.UtcNow;
        try
        {
            outcome = lineExtractionService.ApplyExtractedLines(
                tenantId, quoteId, extractResult.Value.PayloadJson, pages.Count, completedAt);
        }
        catch (JsonException ex)
        {
            // The gateway does not validate the model's output against the schema it was given
            // (IAiGateway.ExtractAsync's own doc comment) — mirrors
            // StagedExtractionService.RunStageAsync's identical guard against malformed JSON from
            // a real (non-fixture) model.
            return await FailAsync(
                quote, job, $"Malformed extraction payload: {ex.Message}", cancellationToken)
                .ConfigureAwait(false);
        }

        // Human-in-the-loop principle: nothing extracted, something skipped, or any line below the
        // confidence threshold all mean a person should look at this quote before it is trusted,
        // even though the AI Gateway call itself succeeded — mirrors
        // StagedExtractionService.RunStageAsync's identical decision rule.
        var finalJobStatus = outcome.ExtractedCount == 0 || outcome.SkippedCount > 0 || outcome.AnyLowConfidence
            ? QuoteExtractionJobStatus.NeedsReview
            : QuoteExtractionJobStatus.Completed;

        if (job is not null)
        {
            job.Status = finalJobStatus;
            job.CompletedAt = completedAt;
        }

        quote.ProcessingStatus = MapStatus(finalJobStatus);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditWriter.WriteAsync(
                new AuditEntry(
                    tenantId,
                    SystemActor,
                    $"quote.extraction.{quote.ProcessingStatus.ToString().ToLowerInvariant()}",
                    "quote",
                    quoteId.Value.ToString(),
                    completedAt),
                cancellationToken)
            .ConfigureAwait(false);

        return Result<QuoteProcessingSummary>.Success(new QuoteProcessingSummary(
            quoteId, quote.ProcessingStatus, outcome.ExtractedCount, outcome.SkippedCount, pages.Count));
    }

    /// <summary>Shared terminal-failure path: marks both rows Failed, persists, and returns the
    /// honest error — never leaves <paramref name="quote"/> looking like extraction is still in
    /// flight (mirrors <c>DocumentProcessingPipeline.ProcessAsync</c>'s identical parse-failure
    /// handling).</summary>
    private async Task<Result<QuoteProcessingSummary>> FailAsync(
        Quote quote, QuoteExtractionJob? job, string error, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        quote.ProcessingStatus = QuoteProcessingStatus.Failed;

        if (job is not null)
        {
            job.Status = QuoteExtractionJobStatus.Failed;
            job.ErrorDetail = Truncate(error);
            job.CompletedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<QuoteProcessingSummary>.Failure(error);
    }

    private static QuoteProcessingStatus MapStatus(QuoteExtractionJobStatus status) => status switch
    {
        QuoteExtractionJobStatus.Completed => QuoteProcessingStatus.Completed,
        QuoteExtractionJobStatus.NeedsReview => QuoteProcessingStatus.NeedsReview,
        QuoteExtractionJobStatus.Failed => QuoteProcessingStatus.Failed,
        _ => QuoteProcessingStatus.Processing,
    };

    /// <summary>Same <c>[[PAGE n]]</c> marker convention as
    /// <c>StagedExtractionService.BuildPageMarkedText</c> (that method is <c>private</c> to its
    /// own module, so this is a small, deliberate, documented duplicate — not a shared helper —
    /// per ADR-002's dependency-direction rule) so a structured-output model can report which page
    /// a line came from by reading these markers.</summary>
    private static string BuildPageMarkedText(IReadOnlyList<DocumentPageText> pages)
    {
        var builder = new StringBuilder();

        foreach (var page in pages)
        {
            builder.Append("[[PAGE ").Append(page.PageNumber).Append("]]\n");
            builder.Append(page.Text);
            builder.Append("\n\n");
        }

        return builder.ToString();
    }

    private static string Truncate(string value, int maxLength = 1000) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>Outcome of one <see cref="QuoteExtractionPipeline.ProcessAsync"/> run — the response
/// shape `POST /api/quotes` (see <see cref="QuotesEndpointExtensions"/>) folds into its own JSON
/// reply.</summary>
internal sealed record QuoteProcessingSummary(
    EntityId QuoteId,
    QuoteProcessingStatus ProcessingStatus,
    int LineItemCount,
    int SkippedCount,
    int PageCount);
