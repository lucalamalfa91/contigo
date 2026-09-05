using Contigo.Quotes.Application;
using Contigo.SharedKernel;

namespace Contigo.Api;

/// <summary>
/// Maps `POST /api/quotes` (product spec Appendix A API table: "Upload/create quote"; parent
/// story us-01-quote-line-extraction AC-1, task E05/F01/US01/T01, quote-extraction). Thin
/// composition per ADR-002 — <see cref="QuoteUploadService"/> owns the upload/storage/audit
/// decisions and <see cref="QuoteExtractionPipeline"/> owns the hybrid-parse/AI-extraction
/// orchestration (see that type's own doc comment for why it, not a domain module, is the AI
/// Gateway call site); this file only translates HTTP &lt;-&gt; those two calls, the same shape
/// <see cref="ContractsEndpointExtensions"/>/<see cref="WorkspaceEndpointExtensions"/> already use.
///
/// Same interim `X-Tenant-Id` header placeholder and multipart `file`-field contract as
/// `POST /api/documents` (see <c>Program.cs</c>'s own comment on that endpoint for why this gap is
/// not promoted to reports/open-questions.md by this task either — ADR-010 is not in this task's
/// "Architecture decisions in force" list, so there is still no validated caller principal to take
/// the tenant from).
/// </summary>
public static class QuotesEndpointExtensions
{
    public static IEndpointRouteBuilder MapQuotesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/quotes", UploadQuoteAsync);
        return endpoints;
    }

    /// <summary>
    /// AC-1 ("`POST /api/quotes` uploads a quote and creates an extraction job"): stores the
    /// uploaded bytes + creates the queued <c>QuoteExtractionJob</c> (<see cref="QuoteUploadService"/>),
    /// then — same "read the bytes once, reuse them for the pipeline" shape `POST /api/documents`
    /// already uses, since an <c>IFormFile</c>'s own stream is not guaranteed re-readable after the
    /// first copy — runs <see cref="QuoteExtractionPipeline"/> synchronously before responding
    /// (AC-2 "line items extract...", AC-4 "reuse the epic-02 hybrid OCR path"). A pipeline
    /// failure is reported honestly in the response (`processingStatus`/`lineItemCount` fall back
    /// to the just-uploaded, pre-processing values) but never turns an already-successful upload
    /// into an HTTP error — the bytes are safely stored and the <c>Quote</c> row already exists
    /// either way (mirrors `POST /api/documents`'s identical posture).
    /// </summary>
    private static async Task<IResult> UploadQuoteAsync(
        HttpRequest request,
        QuoteUploadService uploadService,
        QuoteExtractionPipeline extractionPipeline,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
            || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
        {
            return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart/form-data with a 'file' field.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest("A non-empty 'file' form field is required.");
        }

        byte[] fileBytes;
        await using (var uploadStream = file.OpenReadStream())
        await using (var buffer = new MemoryStream())
        {
            await uploadStream.CopyToAsync(buffer, cancellationToken);
            fileBytes = buffer.ToArray();
        }

        var tenantId = new TenantId(tenantGuid);

        using var storageContent = new MemoryStream(fileBytes);
        var result = await uploadService.UploadAsync(
            tenantId, file.FileName, file.ContentType, storageContent, cancellationToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(result.Error);
        }

        var uploaded = result.Value;

        var processingResult = await extractionPipeline.ProcessAsync(
            tenantId, uploaded.QuoteId, uploaded.FileName, uploaded.MimeType, fileBytes, cancellationToken);

        var processingStatus = processingResult.IsSuccess
            ? processingResult.Value.ProcessingStatus
            : uploaded.ProcessingStatus;
        var lineItemCount = processingResult.IsSuccess ? processingResult.Value.LineItemCount : 0;

        return Results.Created($"/api/quotes/{uploaded.QuoteId}", new
        {
            id = uploaded.QuoteId.Value,
            fileName = uploaded.FileName,
            mimeType = uploaded.MimeType,
            processingStatus = processingStatus.ToString(),
            lineItemCount,
            createdAt = uploaded.CreatedAt,
        });
    }
}
