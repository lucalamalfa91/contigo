using Contigo.SharedKernel;

namespace Contigo.Renewals.Tests.TestSupport;

/// <summary>
/// Fake <see cref="IAuditWriter"/> that records every entry written. Same shape and name as
/// <c>Contigo.Chat.Tests.TestSupport.RecordingAuditWriter</c> /
/// <c>Contigo.AiGateway.Tests.TestSupport.RecordingAuditWriter</c> — a lightweight in-memory spy is
/// enough to prove <see cref="Contigo.Renewals.Application.RenewalThresholdScheduler"/> writes the
/// right <c>renewal.approaching</c> entry without standing up a real Postgres-backed
/// <c>Contigo.Audit.Infrastructure.AuditWriter</c>.
/// </summary>
public sealed class RecordingAuditWriter : IAuditWriter
{
    public List<AuditEntry> Written { get; } = [];

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Written.Add(entry);
        return Task.CompletedTask;
    }
}
