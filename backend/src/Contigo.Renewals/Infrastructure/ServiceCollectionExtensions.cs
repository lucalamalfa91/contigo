using Contigo.Renewals.Application;
using Contigo.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Renewals.Infrastructure;

/// <summary>
/// Composition-root wiring for the Renewals module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a host
/// directly). Task E03/F01/US01/T01 (deterministic-dates) added <see cref="RenewalEngine"/>; task
/// E03/F01/US01/T02 (renewal-opportunity) adds <see cref="RenewalOpportunityGenerator"/> on top of
/// it. No host endpoint takes a dependency on either yet —
/// <c>Contigo.Api.csproj</c>/<c>Contigo.Worker.csproj</c> already carry a <c>ProjectReference</c> to
/// <c>Contigo.Renewals.csproj</c> in anticipation of it, the same "wiring lands with the first real
/// caller" sequencing <c>Contigo.Chat.Infrastructure.ServiceCollectionExtensions</c>'s own doc
/// comment describes for that module. This method exists now so the remaining tasks that depend on
/// <c>renewal-engine</c> (priority score, the threshold scheduler) can resolve
/// <see cref="RenewalEngine"/> from a container instead of constructing it by hand, and so anything
/// that depends on <c>renewal-opportunity</c> (today: only the r2-integration task) can resolve
/// <see cref="RenewalOpportunityGenerator"/> the same way.
/// directly). Task E03/F01/US01/T01 (deterministic-dates) adds <see cref="RenewalEngine"/> but no
/// host endpoint takes a dependency on it yet — <c>Contigo.Api.csproj</c>/<c>Contigo.Worker.csproj</c>
/// already carry a <c>ProjectReference</c> to <c>Contigo.Renewals.csproj</c> in anticipation of it,
/// the same "wiring lands with the first real caller" sequencing
/// <c>Contigo.Chat.Infrastructure.ServiceCollectionExtensions</c>'s own doc comment describes for
/// that module. This method exists now so the three tasks that depend on <c>renewal-engine</c>
/// (renewal-opportunity generation, priority score, the threshold scheduler) can resolve
/// <see cref="RenewalEngine"/> from a container instead of constructing it by hand. Task
/// E03/F01/US02/T01 (priority-score) adds <see cref="PriorityScoreCalculator"/> the same way, for
/// the same reason — still no host endpoint takes a dependency on it yet either.
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

        // Same Scoped lifetime, same rationale: RenewalOpportunityGenerator is itself stateless
        // (only RenewalEngine, itself Scoped) — it just depends on RenewalEngine's registration
        // above, so registration order within this method does not matter to the container (Scoped
        // services resolve their own dependencies lazily, per scope).
        services.AddScoped<RenewalOpportunityGenerator>();
        // PriorityScoreCalculator has no constructor dependencies at all (unlike RenewalEngine, it
        // does not even need IClock — every date-derived input, DaysUntilRenewal, arrives already
        // computed on the RenewalCalculationResult its Calculate method takes). Scoped anyway, for
        // the same uniform-lifetime reason as RenewalEngine above, not because it is stateful.
        services.AddScoped<PriorityScoreCalculator>();

        return services;
    }
}
