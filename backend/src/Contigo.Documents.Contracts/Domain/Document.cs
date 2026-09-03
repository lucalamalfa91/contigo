using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// An uploaded file (contract, amendment, quote, etc. — product spec §6 "Document" row and
/// §7.1 asynchronous ingestion pipeline). The byte content lives in tenant-prefixed object
/// storage (ADR-009); this row is the relational pointer plus processing state. Content history
/// is tracked via <see cref="DocumentVersion"/> — <see cref="StoragePath"/> points at the
/// current version only and is never silently repointed without a new version row
/// (Appendix C rule 5: never destructively overwrite contract history).
/// </summary>
public sealed class Document : TenantScopedEntity
{
    /// <summary>Contract this document belongs to, once classified/linked. Null while a freshly
    /// uploaded document is still being classified (spec §7.1 "uploaded" -&gt; "processing").</summary>
    public EntityId? ContractId { get; set; }

    public required string FileName { get; set; }
    public required string MimeType { get; set; }
    public ContractDocumentType DocumentType { get; set; } = ContractDocumentType.Other;

    /// <summary>Tenant-prefixed object storage path of the current version (ADR-009).</summary>
    public required string StoragePath { get; set; }
    public required string Checksum { get; set; }

    public DocumentProcessingStatus ProcessingStatus { get; set; } = DocumentProcessingStatus.Uploaded;

    public required DateTimeOffset CreatedAt { get; set; }
}
