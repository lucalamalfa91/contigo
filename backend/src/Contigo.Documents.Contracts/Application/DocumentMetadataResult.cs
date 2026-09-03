using Contigo.Documents.Contracts.Domain;
using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Application;

/// <summary>
/// Outcome of a successful <see cref="DocumentQueryService.GetByIdAsync"/> call — the metadata
/// and processing status <see cref="DocumentUploadService"/> persisted for a
/// <see cref="Document"/> (us-01-document-upload, AC-2/AC-3).
/// </summary>
public sealed record DocumentMetadataResult(
    EntityId DocumentId,
    EntityId? ContractId,
    string FileName,
    string MimeType,
    ContractDocumentType DocumentType,
    DocumentProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt);
