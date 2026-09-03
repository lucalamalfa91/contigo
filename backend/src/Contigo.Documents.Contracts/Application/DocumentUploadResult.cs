using Contigo.Documents.Contracts.Domain;
using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Application;

/// <summary>Outcome of a successful <see cref="DocumentUploadService.UploadAsync"/> call.</summary>
public sealed record DocumentUploadResult(
    EntityId DocumentId,
    string FileName,
    string MimeType,
    DocumentProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt);
