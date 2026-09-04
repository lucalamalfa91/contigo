using Contigo.Renewals.Application;
using Contigo.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Renewals.Infrastructure;

/// <summary>
/// Composition-root wiring for the Renewals module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a host
/// directly). Task E03/F01/US01/T01 (deterministic-dates) added <see cref="RenewalEngine"/>;
/// task E03/F03/US01/T01 (renewal-dashboard, this task) adds <see cref="RenewalPipelineBuilder"/>
/// and is the first real caller — <c>Contigo.Api.Program</c> now calls
/// <see cref="AddRenewalsModule"/> and maps <c>GET /api/renewals</c>
/// (<c>Contigo.Api.RenewalsEndpointExtensions</c>), the same "wiring lands with the first real
/// caller" sequencing <c>Contigo.Chat.Infrastructure.ServiceCollectionExtensions</c>'s own doc
/// comment describes for that module. <c>Contigo.Worker.csproj</c> still only carries a
/// <c>ProjectReference</c> to <c>Contigo.Renewals.csproj</c> in anticipation — no worker job calls
/// this module yet (the threshold scheduler remains a follow-up task).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRenewalsModule(this IServiceCollection services)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first registration
        // wins, and every module shares the same "now" (IClock) — mirrors
        // Contigo.Chat.Infrastructure.ServiceCollectionExtensions.AddChatModule /
        // Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule /
        // Contigo.Documents.Contracts.Infrastructure.ServiceCollectionExtensions
        // .AddDocumentsContractsModule.
        services.TryAddSingleton<IClock, SystemClock>();

        // Scoped, not Singleton: RenewalEngine itself is stateless (only IClock, itself
        // Singleton), but Scoped is the uniform per-request/job lifetime every other module's own
        // AddXxxModule already picks for its services (see Contigo.Chat's own doc comment on this
        // exact choice) — a future dependency with a narrower lifetime (a Scoped DbContext, for
        // example, once opportunity persistence lands) then does not force a re-registration here.
        services.AddScoped<RenewalEngine>();

        // RenewalPipelineBuilder (task E03/F03/US01/T01) only depends on RenewalEngine + IClock,
        // both already registered above — same Scoped lifetime for the same reason.
        services.AddScoped<RenewalPipelineBuilder>();

        return services;
    }
}
