using Contigo.Audit.Infrastructure;
using Contigo.Documents.Contracts.Infrastructure;
using Contigo.Worker.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Worker;

/// <summary>
/// Composition-root wiring for the Worker host (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method", applied here to the host itself). Single source
/// of truth for what <c>Program.cs</c> registers — Contigo.Worker.Tests calls this same method,
/// so the deployable-worker tests prove what the host actually boots with, not a parallel
/// hand-rolled container (mirrors Contigo.Api.Tests.DeployableApiTests' approach for the API
/// host).
/// </summary>
public static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Wires the Worker host: the same Documents/Contracts application services the API host
    /// composes (parent story us-04 AC-2 "references the same application services"), plus the
    /// queue-consumption hosted service (AC-2 "... and consumes the queue"; ADR-002 "queue
    /// message handlers belong to the worker host, not to domain projects").
    ///
    /// Also wires the Audit module (task E01/F09/US01/T01, r0-integration): task E01/F06/US01/T01
    /// gave <c>DocumentUploadService</c> a required <c>IAuditWriter</c> dependency, and
    /// <c>AddDocumentsContractsModule</c> registers that service in *any* host that calls it —
    /// including this one (<c>Contigo.Worker.csproj</c> already referenced <c>Contigo.Audit</c>
    /// before this task, anticipating exactly this). Without this call, the Worker's container
    /// would hold a registered <c>DocumentUploadService</c> whose own dependency graph can never
    /// resolve — harmless while nothing in this host asks for it (today), but a landmine under
    /// any host-builder configuration that validates the DI graph eagerly
    /// (<c>ServiceProviderOptions.ValidateOnBuild</c>, on by default whenever
    /// <c>DOTNET_ENVIRONMENT=Development</c>). Keeping both hosts' module composition in lockstep
    /// is also what this method's own doc comment already promises ("the same application
    /// services") — future worker jobs (extraction, renewal recompute) will need
    /// <c>IAuditWriter</c> for their own audit writes anyway (ADR-011).
    /// </summary>
    public static IServiceCollection AddWorkerHost(
        this IServiceCollection services, string documentsContractsConnectionString, string auditConnectionString)
    {
        services.AddDocumentsContractsModule(documentsContractsConnectionString);
        services.AddAuditModule(auditConnectionString);

        services.AddSingleton<InMemoryQueueConsumer>();
        services.AddSingleton<IQueueConsumer>(sp => sp.GetRequiredService<InMemoryQueueConsumer>());
        services.AddHostedService<QueueConsumerHostedService>();

        return services;
    }
}
