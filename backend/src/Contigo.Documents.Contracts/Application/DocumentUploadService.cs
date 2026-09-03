using System.Security.Cryptography;
using Contigo.Documents.Contracts.Domain;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Storage;
using Contigo.SharedKernel.Tenancy;

namespace Contigo.Documents.Contracts.Application;

/// <summary>
/// Implements task E01/F06/US01/T01 (us-01-document-upload, AC-1/AC-2): stores the uploaded
/// bytes in tenant-scoped object storage (no cross-tenant path) and persists the
/// <see cref="Document"/> + initial <see cref="DocumentVersion"/> + a queued classification
/// <see cref="ExtractionJob"/> as one unit of work (module-map "Worker responsibilities":
/// classification is the first extraction stage after upload).
///
/// Owns its own tenant scope (<see cref="ITenantContext.BeginScope"/>) for the duration of the
/// call instead of relying on the caller to have entered one — every caller (the API endpoint
/// today, a queue handler later) gets the ADR-009 RLS backstop automatically, and a caller that
/// also wraps this in its own scope is unaffected (nested scopes restore the previous value on
/// dispose).
/// </summary>
public sealed class DocumentUploadService(
    DocumentsContractsDbContext dbContext,
    IDocumentStorage storage,
    ITenantContext tenantContext,
    IClock clock)
{
    private const int InitialVersionNumber = 1;

    /// <summary>
    /// Placeholder actor recorded on <see cref="DocumentVersion.CreatedBy"/> until the API
    /// validates a caller identity token. ADR-010 (Entra ID/OIDC) is not listed in this task's
    /// "Architecture decisions in force" — there is no validated caller principal yet, so there
    /// is nothing truthful to record here beyond this explicit placeholder.
    /// </summary>
    private const string UnattributedActor = "unattributed";

    public async Task<Result<DocumentUploadResult>> UploadAsync(
        TenantId tenantId,
        string fileName,
        string? mimeType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result<DocumentUploadResult>.Failure("A file name is required.");
        }

        // Buffered once: the checksum needs the full byte range, and the storage write needs a
        // seekable, replayable stream regardless of what kind of stream the caller handed in (an
        // ASP.NET Core request body stream is not guaranteed seekable).
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length == 0)
        {
            return Result<DocumentUploadResult>.Failure("The uploaded file is empty.");
        }

        buffer.Position = 0;
        var checksum = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));

        var documentId = EntityId.New();
        var now = clock.UtcNow;
        var effectiveMimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType;

        using var tenantScope = tenantContext.BeginScope(tenantId);

        buffer.Position = 0;
        var storagePath = await storage
            .SaveAsync(tenantId, documentId, InitialVersionNumber, fileName, buffer, cancellationToken)
            .ConfigureAwait(false);

        var document = new Document
        {
            Id = documentId,
            TenantId = tenantId,
            FileName = fileName,
            MimeType = effectiveMimeType,
            StoragePath = storagePath,
            Checksum = checksum,
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            CreatedAt = now,
        };

        var version = new DocumentVersion
        {
            TenantId = tenantId,
            DocumentId = documentId,
            VersionNumber = InitialVersionNumber,
            StoragePath = storagePath,
            Checksum = checksum,
            CreatedBy = UnattributedActor,
            CreatedAt = now,
        };

        // First stage of the extraction pipeline (module-map "Worker responsibilities"): the
        // Worker host picks this up and classifies the document type before any further
        // extraction stage runs. Only the queued job row is created here — dequeue/consumption
        // is the Worker's concern and is not part of this task.
        var classificationJob = new ExtractionJob
        {
            TenantId = tenantId,
            DocumentId = documentId,
            Stage = ExtractionStage.Classification,
            Status = ExtractionJobStatus.Queued,
            QueuedAt = now,
        };

        dbContext.Documents.Add(document);
        dbContext.DocumentVersions.Add(version);
        dbContext.ExtractionJobs.Add(classificationJob);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<DocumentUploadResult>.Success(new DocumentUploadResult(
            document.Id, document.FileName, document.MimeType, document.ProcessingStatus, document.CreatedAt));
    }
}
