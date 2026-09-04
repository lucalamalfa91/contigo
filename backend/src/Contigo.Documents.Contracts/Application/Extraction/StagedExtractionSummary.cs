using Contigo.Documents.Contracts.Domain;
using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Application.Extraction;

/// <summary>Outcome of one <see cref="StagedExtractionService.RunAsync"/> call: the
/// <see cref="Domain.Contract"/> facts were staged into, the document's resulting
/// <see cref="DocumentProcessingStatus"/>, and each stage's own result (AC-1: every stage runs
/// and reports independently — one stage failing does not hide the others' outcomes).</summary>
public sealed record StagedExtractionSummary(
    EntityId ContractId,
    DocumentProcessingStatus DocumentProcessingStatus,
    IReadOnlyList<StagedExtractionStageResult> Stages);

/// <summary>Result of running a single <see cref="ExtractionStage"/> (one <see cref="ExtractionJob"/>
/// row). <see cref="ExtractedCount"/> is how many facts/items were persisted;
/// <see cref="SkippedCount"/> is how many the model returned but could not be persisted (missing
/// a required field, an unparseable enum/date) — a non-zero <see cref="SkippedCount"/> or a
/// <see cref="Status"/> of <see cref="ExtractionJobStatus.NeedsReview"/>/
/// <see cref="ExtractionJobStatus.Failed"/> means a human should look at this stage
/// (product principle: "Human-in-the-loop for consequential decisions... low-confidence
/// extraction... must be reviewable").</summary>
public sealed record StagedExtractionStageResult(
    ExtractionStage Stage,
    ExtractionJobStatus Status,
    int ExtractedCount,
    int SkippedCount,
    string? ErrorDetail);
