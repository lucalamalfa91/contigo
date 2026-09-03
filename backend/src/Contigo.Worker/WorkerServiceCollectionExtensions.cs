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
    /// </summary>
    public static IServiceCollection AddWorkerHost(
        this IServiceCollection services, string documentsContractsConnectionString)
    {
        services.AddDocumentsContractsModule(documentsContractsConnectionString);

        services.AddSingleton<InMemoryQueueConsumer>();
        services.AddSingleton<IQueueConsumer>(sp => sp.GetRequiredService<InMemoryQueueConsumer>());
        services.AddHostedService<QueueConsumerHostedService>();

        return services;
    }
}
