using Azure.Storage.Blobs;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Storage;

namespace Contigo.Api.Infrastructure;

/// <summary>
/// Azure Blob Storage-backed <see cref="IDocumentStorage"/> (ADR-005 "Object storage" row,
/// ADR-011). This is the one place in the API host that touches the Azure Storage SDK — domain
/// modules only ever see <see cref="IDocumentStorage"/> (ADR-002: no provider SDK in domain
/// code). Internal: composition-root infrastructure, not a public host type
/// (Contigo.ArchitectureTests' host-purity rule only inspects public types).
/// </summary>
internal sealed class AzureBlobDocumentStorage(BlobContainerClient container) : IDocumentStorage
{
    public async Task<string> SaveAsync(
        TenantId tenantId,
        EntityId documentId,
        int versionNumber,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var path = DocumentStoragePath.Build(tenantId, documentId, versionNumber, fileName);

        // Real Azure already has this container from Terraform
        // (infra/modules/storage/main.tf: azurerm_storage_container.documents) — this is a fast,
        // idempotent no-op there. Local Azurite does not pre-create it, so this is what makes a
        // bare `azurite` dev container work with zero manual setup. Checked on every call rather
        // than once at DI-registration time so that resolving IDocumentStorage never itself
        // requires a live storage connection (mirrors how resolving a DbContext never opens a
        // connection until a query actually runs).
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var blob = container.GetBlobClient(path);

        // Each (tenant, document, version) triple is only ever written once by
        // DocumentUploadService (a new upload always mints a fresh document id and starts at
        // version 1), so overwrite:true is a safety net for retries, not an expected collision.
        await blob.UploadAsync(content, overwrite: true, cancellationToken).ConfigureAwait(false);

        return path;
    }
}
