using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Audit.Infrastructure;

/// <summary>
/// Composition-root wiring for the Audit module. ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a host
/// directly. Registers both the DbContext (with the RLS backstop already live, same as every
/// other module) and this module's <see cref="IAuditWriter"/> implementation, so any future host
/// wiring — or any other module's own `AddXxx` — only needs to call
/// <see cref="AddAuditModule"/> once to get a working, tenant-scoped, append-only
/// <see cref="IAuditWriter"/> from DI. No endpoint/queue handler calls this yet (that is task
/// E01/F06/US02/T02's job for the query side; write-side callers arrive as later tasks retrofit
/// their own modules to call <see cref="IAuditWriter"/>), so whichever future task adds the first
/// caller only has to take a dependency on <see cref="IAuditWriter"/> — everything else is
/// already wired.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuditModule(
        this IServiceCollection services, string connectionString)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first
        // registration wins, and every module shares the same ambient tenant claim (ADR-009).
        services.TryAddSingleton<ITenantContext, TenantContext>();

        services.AddDbContext<AuditDbContext>(
            (sp, options) => AuditDbContextOptions.Configure(
                options, connectionString, sp.GetRequiredService<ITenantContext>()));

        services.AddScoped<IAuditWriter, AuditWriter>();

        return services;
    }
}
