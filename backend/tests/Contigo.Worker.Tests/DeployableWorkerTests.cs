using Contigo.Documents.Contracts.Infrastructure;
using Contigo.SharedKernel.Tenancy;
using Contigo.Worker.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Contigo.Worker.Tests;

/// <summary>
/// Proves the Definition of Done for task E01/F04/US04/T02 (deployable-worker, ADR-002): the
/// worker host actually boots as a composition root, references the same Documents/Contracts
/// application services the API host does (parent story us-04 AC-2 "Worker host references the
/// same application services"), and its background service really drains a message off the
/// queue end-to-end (AC-2 "... and consumes the queue") — not just left the "module registration
/// will go here" placeholder from the solution scaffold (E01/F04/US01/T01).
/// </summary>
public sealed class DeployableWorkerTests
{
    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        // A syntactically valid Npgsql connection string satisfies AddDocumentsContractsModule's
        // eager UseNpgsql() parsing. Nothing below opens a real connection, so no running
        // Postgres instance is required for this test (same approach as
        // Contigo.Api.Tests.DeployableApiTests).
        builder.Services.AddWorkerHost(
            "Host=localhost;Port=5432;Database=contigo_dev;Username=contigo;Password=contigo;Include Error Detail=true");

        return builder.Build();
    }

    [Fact]
    public void Host_composes_the_documents_contracts_module_into_di()
    {
        using var host = BuildHost();
        using var scope = host.Services.CreateScope();

        // AC-2 ("Worker host references the same application services"): resolve the same
        // module DbContext and shared tenant claim the API host composes
        // (Contigo.Api.Tests.DeployableApiTests.Host_composes_the_documents_contracts_module_into_di)
        // out of the worker's own real service provider, not a hand-rolled stand-in container.
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentsContractsDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        Assert.NotNull(dbContext);
        Assert.NotNull(tenantContext);
    }

    [Fact]
    public void Host_registers_the_queue_consumer_hosted_service()
    {
        using var host = BuildHost();

        // AC-2 ("... and consumes the queue"): a hosted service is actually registered to drive
        // the queue, not just the queue port sitting unused.
        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service => service is QueueConsumerHostedService);
    }

    [Fact]
    public async Task Worker_consumes_a_queued_message_end_to_end()
    {
        using var host = BuildHost();
        var queueConsumer = host.Services.GetRequiredService<InMemoryQueueConsumer>();
        var message = new QueueMessage(Guid.NewGuid().ToString(), "extraction-job-queued");

        queueConsumer.Enqueue(message);

        await host.StartAsync();
        try
        {
            // QueueConsumerHostedService polls on a background thread (200ms delay between empty
            // receives); bound the wait instead of asserting immediately.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!queueConsumer.CompletedMessageIds.Contains(message.MessageId) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }

            Assert.Contains(message.MessageId, queueConsumer.CompletedMessageIds);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
