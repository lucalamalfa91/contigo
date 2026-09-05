using Contigo.SharedKernel;

namespace Contigo.Quotes.Domain;

/// <summary>
/// An uploaded supplier quote file (task E05/F01/US01/T01, quote-extraction; parent story
/// us-01-quote-line-extraction AC-1 "POST /api/quotes uploads a quote and creates an extraction
/// job"; product spec §4.4/§11 "New Purchase / Quote Check"; module-map.md "Quotes | Quote,
/// QuoteLine, Assessment, NegotiationOutcome | /api/quotes"). Deliberately the same
/// upload-metadata shape as <c>Contigo.Documents.Contracts.Domain.Document</c> (filename/mime
/// type/storage path/checksum/processing status) rather than a reference to that type: ADR-002
/// forbids this module from referencing <c>Contigo.Documents.Contracts</c> at all
/// (<c>Contigo.ArchitectureTests.DependencyDirectionTests</c>'s allow-list for
/// <c>Contigo.Quotes</c> is exactly <c>[SharedKernel, Benchmark]</c>), and a quote is not a
/// contract — reusing <c>Document</c>/<c>Contract</c> would also route every uploaded quote
/// through <c>StagedExtractionService.EnsureContractAsync</c>, silently creating a phantom
/// <c>Contract</c> row for a document that is never a contract (product spec §11's own Quote →
/// Benchmark → Assessment → Negotiate → Contract flow treats "becomes a Contract" as a later,
/// explicit step, not an upload-time side effect).
///
/// <para>
/// Deliberately does <b>not</b> yet carry the spec §6 Quote-level aggregate fields ("supplier,
/// dates, currency, values, status") this module-map row also names — this task's own coding
/// objective is "Quote upload + line-item extraction" (parent story task table: task-01), and
/// us-01's own task-02 ("Line-item normalization + evidence/confidence") is the next task in this
/// story. Supplier/currency/status aggregation reads naturally as a later normalization/assessment
/// concern once line items exist to aggregate — inventing those columns ahead of a real writer
/// would be exactly the "fabricated precision" Appendix C rule 10 warns against, the same
/// "documented, not invented ahead of need" restraint <c>Contigo.Savings.Domain
/// .SavingsOpportunity</c>'s own doc comment already applies to its still-missing <c>QuoteId</c>
/// column.
/// </para>
/// </summary>
public sealed class Quote : TenantScopedEntity
{
    public required string FileName { get; set; }
    public required string MimeType { get; set; }

    /// <summary>Tenant-prefixed object storage path (ADR-009) — built via the same
    /// <c>Contigo.SharedKernel.Storage.DocumentStoragePath.Build</c> helper
    /// <c>Contigo.Documents.Contracts.Application.DocumentUploadService</c> uses; that helper is
    /// already generic over "an uploaded document's id", not specific to
    /// <c>Contigo.Documents.Contracts.Domain.Document</c>.</summary>
    public required string StoragePath { get; set; }

    public required string Checksum { get; set; }

    public QuoteProcessingStatus ProcessingStatus { get; set; } = QuoteProcessingStatus.Uploaded;

    public required DateTimeOffset CreatedAt { get; set; }
}
