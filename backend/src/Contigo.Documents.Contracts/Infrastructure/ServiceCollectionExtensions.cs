using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Documents.Contracts.Infrastructure;

/// <summary>
/// Composition-root wiring for the Documents/Contracts module. ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a
/// host directly. The Api/Worker hosts will call this once they take a dependency on this
/// module — not wired up yet in this task (host composition + the ambient tenant claim are
/// us-03/us-04's job; this task's file scope is this module's Infrastructure/Migrations only).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsContractsModule(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DocumentsContractsDbContext>(
            options => DocumentsContractsDbContextOptions.Configure(options, connectionString));

        return services;
    }
}
