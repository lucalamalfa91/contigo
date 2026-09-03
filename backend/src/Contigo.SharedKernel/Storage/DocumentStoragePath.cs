namespace Contigo.SharedKernel.Storage;

/// <summary>
/// Builds the tenant-prefixed object storage path every <see cref="IDocumentStorage"/>
/// implementation must use (ADR-009). Centralised here so the path scheme is defined exactly
/// once: no implementation constructs a path by hand, so none can accidentally omit or
/// mis-order the tenant prefix, and two different tenants can never collide on the same path.
/// </summary>
public static class DocumentStoragePath
{
    public static string Build(TenantId tenantId, EntityId documentId, int versionNumber, string fileName)
    {
        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(versionNumber), versionNumber, "Version number must be 1 or greater.");
        }

        return $"{tenantId.Value:D}/documents/{documentId.Value:D}/v{versionNumber}/{Sanitize(fileName)}";
    }

    /// <summary>
    /// Strips path-separator characters from the file name component so it can never introduce
    /// extra "virtual directory" segments into the blob path (for example a client-supplied name
    /// containing <c>/</c> or <c>\</c>), and falls back to a generic name when blank. Fixed,
    /// platform-independent rules only — <see cref="Path.GetInvalidFileNameChars"/> is
    /// deliberately not used here, since it varies by host OS and this governs a cloud blob key,
    /// not a real filesystem path.
    /// </summary>
    private static string Sanitize(string fileName)
    {
        var trimmed = (fileName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "file";
        }

        var chars = new char[trimmed.Length];
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            chars[i] = c is '/' or '\\' ? '_' : c;
        }

        return new string(chars);
    }
}
