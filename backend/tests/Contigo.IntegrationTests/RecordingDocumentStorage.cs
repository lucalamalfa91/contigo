using Contigo.SharedKernel;
using Contigo.SharedKernel.Storage;

namespace Contigo.IntegrationTests;

/// <summary>
/// Fake <see cref="IDocumentStorage"/> for <see cref="R0IntegrationFixture"/> — proves the
/// "storage" step of task E01/F09/US01/T01's AC-1 without a real Azure Blob/Azurite dependency,
/// mirroring <c>Contigo.Documents.Contracts.Tests.DocumentUploadServiceTests</c>'s own test double
/// of the same shape (ADR-005/ADR-011: domain code only ever sees <see cref="IDocumentStorage"/>,
/// so substituting the adapter at the test host's DI level is the sanctioned way to prove this
/// step without a real cloud dependency).
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
