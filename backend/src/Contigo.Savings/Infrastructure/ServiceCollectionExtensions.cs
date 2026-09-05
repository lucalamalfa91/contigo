using Contigo.Savings.Application;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Savings.Infrastructure;

/// <summary>
/// Composition-root wiring for the Savings module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"). Task E04/F02/US02/T01 (savings-opportunity) gives
/// this module its first <c>DbContext</c> (<see cref="SavingsDbContext"/>, backing
/// <see cref="SavingsOpportunityService"/>) —
/// <c>Contigo.Savings.Application.PriceNormalizationCalculator</c> (task E04/F02/US01/T01) needed no
/// registration at all (a stateless, dependency-free calculator a caller `new`s up directly), so
/// this is this module's first `AddXxxModule` call of any kind — the same "wiring lands with the
/// first real caller" gap this module's own README section used to name, now closed the same way
/// `Contigo.Renewals`'s own <c>RenewalAction</c> closed it there.
///
/// <see cref="ITenantContext"/>/<see cref="TenantContext"/> registration mirrors
/// <c>Contigo.Renewals.Infrastructure.ServiceCollectionExtensions.AddRenewalsModule</c>'s own
/// `TryAddSingleton` exactly — the same ambient tenant claim every module's DbContext shares
/// (ADR-009), so whichever module's `AddXxxModule` runs first in a given host wins the single
/// registration, harmlessly, for every module after it. Any host that calls this method must also
/// call `Contigo.Audit`'s `AddAuditModule` for <see cref="SavingsOpportunityService"/>'s own
/// <see cref="IAuditWriter"/> dependency to resolve — the same landmine
/// <c>RenewalActionService</c>'s own registration already flags.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSavingsModule(this IServiceCollection services, string connectionString)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first registration
        // wins, and every module shares the same "now" (IClock) — mirrors
        // Contigo.Renewals.Infrastructure.ServiceCollectionExtensions.AddRenewalsModule /
        // Contigo.Chat.Infrastructure.ServiceCollectionExtensions.AddChatModule.
        services.TryAddSingleton<IClock, SystemClock>();

        // Same ambient tenant claim every module's DbContext shares (ADR-009) — see the type doc
        // comment.
        services.TryAddSingleton<ITenantContext, TenantContext>();

        services.AddDbContext<SavingsDbContext>(
            (sp, options) => SavingsDbContextOptions.Configure(
                options, connectionString, sp.GetRequiredService<ITenantContext>()));

        // SavingsOpportunityService depends on the Scoped SavingsDbContext above plus IAuditWriter —
        // any host that calls this method must also call Contigo.Audit's AddAuditModule for that to
        // resolve (see the type doc comment).
        services.AddScoped<SavingsOpportunityService>();

        return services;
    }
}
