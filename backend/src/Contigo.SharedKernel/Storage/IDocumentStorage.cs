namespace Contigo.SharedKernel.Storage;

/// <summary>
/// Tenant-scoped binary object storage for uploaded documents (ADR-005 "Object storage" row,
/// ADR-009, ADR-011). Domain modules depend on this interface only — the concrete adapter
/// (Azure Blob Storage in deployed environments) is wired by the host composition root, so no
/// domain module ever references a cloud storage SDK directly (ADR-002: domain modules must not
/// reference a provider SDK).
///
/// Every implementation MUST derive the stored path from <see cref="DocumentStoragePath"/> (or
/// an equivalent tenant-prefixing scheme) — the path is never accepted from a caller as a raw
/// string. ADR-009: "Object-storage paths must be tenant-prefixed ... issued through a
/// server-side path governed by the same tenant claim, never a client-supplied raw blob URL."
/// </summary>
public interface IDocumentStorage
{
    /// <summary>
    /// Persists <paramref name="content"/> under a tenant-prefixed path derived from
    /// <paramref name="tenantId"/>, <paramref name="documentId"/>, <paramref name="versionNumber"/>
    /// and <paramref name="fileName"/>, and returns the path that was actually written to.
    /// </summary>
    Task<string> SaveAsync(
        TenantId tenantId,
        EntityId documentId,
        int versionNumber,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}
