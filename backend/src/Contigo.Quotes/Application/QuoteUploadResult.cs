using Contigo.Quotes.Domain;
using Contigo.SharedKernel;

namespace Contigo.Quotes.Application;

/// <summary>Result of <see cref="QuoteUploadService.UploadAsync"/> — mirrors
/// <c>Contigo.Documents.Contracts.Application.DocumentUploadResult</c>'s own shape.</summary>
public sealed record QuoteUploadResult(
    EntityId QuoteId,
    string FileName,
    string MimeType,
    QuoteProcessingStatus ProcessingStatus,
    DateTimeOffset CreatedAt);
