using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Contigo.Documents.Contracts.Application;

/// <summary>
/// Implements task E01/F06/US01/T02 (us-01-document-upload, AC-3): reads back the document
/// metadata + processing status that <see cref="DocumentUploadService"/> already persists
/// (AC-2), scoped to the caller's tenant.
///
/// Opens its own <see cref="ITenantContext.BeginScope"/> for the tenant it is given (same
/// rationale as <c>WorkspaceMembershipService</c>: an inherited-but-wrong ambient scope should
/// not silently read another tenant's row), and filters explicitly by <see cref="TenantId"/> in
/// the query itself on top of that scope. ADR-009 treats Postgres Row-Level Security as the
/// *non-bypassable backstop*, not the only check — a document belonging to a different tenant
/// therefore reads back as "not found" for two independent reasons (this predicate and RLS), not
/// just one.
/// </summary>
public sealed class DocumentQueryService(DocumentsContractsDbContext dbContext, ITenantContext tenantContext)
{
    public async Task<DocumentMetadataResult?> GetByIdAsync(
        TenantId tenantId, EntityId documentId, CancellationToken cancellationToken = default)
    {
        using var tenantScope = tenantContext.BeginScope(tenantId);

        var document = await dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.TenantId == tenantId && d.Id == documentId, cancellationToken)
            .ConfigureAwait(false);

        return document is null
            ? null
            : new DocumentMetadataResult(
                document.Id,
                document.ContractId,
                document.FileName,
                document.MimeType,
                document.DocumentType,
                document.ProcessingStatus,
                document.CreatedAt);
    }
}
