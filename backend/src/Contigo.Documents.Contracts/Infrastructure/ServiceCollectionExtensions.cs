using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Documents.Contracts.Infrastructure;

/// <summary>
/// Composition-root wiring for the Documents/Contracts module. ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a
/// host directly. The Api/Worker hosts will call this once they take a dependency on this
/// module — not wired up yet (no endpoint/queue handler exists to attach a tenant claim to;
/// that is us-04's job). This task (us-03) wires the ambient tenant claim
/// (<see cref="ITenantContext"/>) and the RLS connection interceptor into the DbContext
/// pipeline itself, so whichever future task adds the first endpoint/handler only has to call
/// <see cref="ITenantContext.BeginScope"/> around it — the RLS backstop is already live.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsContractsModule(
        this IServiceCollection services, string connectionString)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first
        // registration wins, and every module shares the same ambient tenant claim (ADR-009).
        services.TryAddSingleton<ITenantContext, TenantContext>();

        services.AddDbContext<DocumentsContractsDbContext>(
            (sp, options) => DocumentsContractsDbContextOptions.Configure(
                options, connectionString, sp.GetRequiredService<ITenantContext>()));

        return services;
    }
}
