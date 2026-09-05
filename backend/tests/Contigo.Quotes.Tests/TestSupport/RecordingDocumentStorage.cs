using Contigo.SharedKernel;
using Contigo.SharedKernel.Storage;

namespace Contigo.Quotes.Tests.TestSupport;

/// <summary>
/// Fake <see cref="IDocumentStorage"/> for <see cref="Contigo.Quotes.Application.QuoteUploadService"/>
/// tests — proves the storage step without a real Azure Blob/Azurite dependency, mirroring
/// <c>Contigo.Documents.Contracts.Tests.DocumentUploadServiceTests</c>'s own test double of the same
/// shape (ADR-005/ADR-011: domain code only ever sees <see cref="IDocumentStorage"/>).
/// </summary>
public sealed class RecordingDocumentStorage : IDocumentStorage
{
    public List<(string Path, byte[] Content)> Saved { get; } = [];

    public async Task<string> SaveAsync(
        TenantId tenantId,
        EntityId documentId,
        int versionNumber,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var path = DocumentStoragePath.Build(tenantId, documentId, versionNumber, fileName);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        Saved.Add((path, buffer.ToArray()));

        return path;
    }
}
