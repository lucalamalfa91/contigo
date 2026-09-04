using Contigo.Documents.Contracts.Application;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Documents.Contracts.Infrastructure;

/// <summary>
/// Composition-root wiring for the Documents/Contracts module. ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a
/// host directly. Task E01/F04/US03/T01 (us-03) wired the ambient tenant claim
/// (<see cref="ITenantContext"/>) and the RLS connection interceptor into the DbContext pipeline
/// itself, so the first endpoint/handler to land only had to call
/// <see cref="ITenantContext.BeginScope"/> around it — the RLS backstop was already live. That
/// first endpoint is task E01/F06/US01/T01's <c>POST /api/documents</c>, wired via
/// <see cref="DocumentUploadService"/>, registered here alongside the DbContext. Task
/// E01/F06/US01/T02's <c>GET /api/documents/{id}</c> reuses the same DbContext registration and
/// adds <see cref="DocumentQueryService"/> alongside it. Task E02/F03/US01/T01's
/// <c>GET /api/contracts</c> reuses it again and adds <see cref="PortfolioQueryService"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsContractsModule(
        this IServiceCollection services, string connectionString)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first
        // registration wins, and every module shares the same ambient tenant claim (ADR-009)
        // and the same "now" (IClock).
        services.TryAddSingleton<ITenantContext, TenantContext>();
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddDbContext<DocumentsContractsDbContext>(
            (sp, options) => DocumentsContractsDbContextOptions.Configure(
                options, connectionString, sp.GetRequiredService<ITenantContext>()));

        // Scoped: shares the request/job's own DbContext instance (also Scoped, via AddDbContext
        // above) rather than a second, independently-tracked context.
        services.AddScoped<DocumentUploadService>();
        services.AddScoped<DocumentQueryService>();
        services.AddScoped<PortfolioQueryService>();

        return services;
    }
}
