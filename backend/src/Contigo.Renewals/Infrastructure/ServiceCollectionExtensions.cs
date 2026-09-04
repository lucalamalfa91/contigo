using Contigo.Renewals.Application;
using Contigo.Renewals.Configuration;
using Contigo.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Renewals.Infrastructure;

/// <summary>
/// Composition-root wiring for the Renewals module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a host
/// directly). Registers every artifact this module has produced so far, task by task:
///
/// <list type="bullet">
/// <item>E03/F01/US01/T01 (deterministic-dates): <see cref="RenewalEngine"/>.</item>
/// <item>E03/F01/US01/T02 (renewal-opportunity): <see cref="RenewalOpportunityGenerator"/>, built
/// on <see cref="RenewalEngine"/>.</item>
/// <item>E03/F01/US02/T01 (priority-score): <see cref="PriorityScoreCalculator"/>.</item>
/// <item>E03/F02/US01/T01 (threshold-scheduler): <see cref="RenewalThresholdScheduler"/> and its
/// <see cref="ThresholdWindowOptions"/> (config section <c>Renewals:Thresholds</c>) — the first
/// real caller is <c>Contigo.Worker.WorkerServiceCollectionExtensions.AddWorkerHost</c>, which now
/// calls this method so its own daily-cadence hosted service can resolve it.</item>
/// <item>E03/F03/US01/T01 (renewal-dashboard): <see cref="RenewalPipelineBuilder"/> — the first
/// real caller is <c>Contigo.Api.Program</c>, which now calls this method and maps
/// <c>GET /api/renewals</c> (<c>Contigo.Api.RenewalsEndpointExtensions</c>).</item>
/// <item>E03/F01/US02/T02 (priority-explainability, this task): <see cref="PriorityScoreWeightsOptions"/>
/// (config section <c>Renewals:PriorityWeights</c>) — <see cref="PriorityScoreCalculator"/>'s
/// registration below is unchanged (still <c>AddScoped</c>), but it now resolves this options
/// singleton as a constructor dependency instead of using its own compile-time defaults. First real
/// caller: <c>Contigo.Api.RenewalsEndpointExtensions</c>'s new <c>GET /api/renewals/{id}/priority</c>
/// route.</item>
/// </list>
///
/// Every "wiring lands with the first real caller" gap this list used to describe is now closed —
/// <c>Contigo.Chat.Infrastructure.ServiceCollectionExtensions</c>'s own doc comment describes the
/// same sequencing for that module. <c>Contigo.Worker.csproj</c> still only carries a
/// <c>ProjectReference</c> to <c>Contigo.Renewals.csproj</c> in anticipation of
/// <see cref="RenewalOpportunityGenerator"/>/<see cref="PriorityScoreCalculator"/>/
/// <see cref="RenewalPipelineBuilder"/> specifically — no worker job calls any of the three yet.
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

        // Task E03/F01/US02/T02 (priority-explainability): PriorityScoreWeightsOptions binds the
        // same "start from IConfiguration, property initializers supply the spec default" way as
        // ThresholdWindowOptions immediately below — every property here is a scalar decimal, so
        // (unlike ThresholdWindowOptions.DaysBeforeDeadline) the plain array-merge footgun that
        // class's own doc comment documents does not apply, and a direct
        // `new PriorityScoreWeightsOptions()` + `section.Bind(options)` is safe as-is. Singleton:
        // this options object is immutable after construction and shared by every
        // PriorityScoreCalculator instance across every scope, the same lifetime
        // ThresholdWindowOptions itself already uses.
        services.TryAddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var options = new PriorityScoreWeightsOptions();
            configuration.GetSection(PriorityScoreWeightsOptions.SectionName).Bind(options);
            return options;
        });

        // PriorityScoreCalculator has no *required* constructor dependency (its one constructor
        // parameter defaults to null, resolved to a spec-default PriorityScoreWeightsOptions
        // internally — see that class's own doc comment) — but the container always finds the
        // PriorityScoreWeightsOptions singleton just registered above and injects it, so a real
        // host's calculator is always the configured one, never the fallback. Scoped anyway, for
        // the same uniform-lifetime reason as RenewalEngine above, not because it is stateful.
        services.AddScoped<PriorityScoreCalculator>();

        // Task E03/F02/US01/T01 (threshold-scheduler): same "bind lazily from IConfiguration,
        // property initializers supply the spec default" pattern
        // Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule already uses for its own
        // options — a deployment with no "Renewals:Thresholds" section configured still gets
        // product spec §9.1's own default ladder (365/270/180/120/90/60/30 days). IConfiguration is
        // always already registered by WebApplicationBuilder / Host.CreateApplicationBuilder, so
        // this method does not need its own IConfiguration parameter threaded through every caller.
        //
        // Deliberately does NOT just `new ThresholdWindowOptions()` then `.Bind(options)` the way
        // AiGatewayModelOptions/AiGatewayOcrOptions do: Microsoft.Extensions.Configuration's array
        // binder appends a section's children onto whatever the target array property already
        // holds rather than replacing it (see ThresholdWindowOptions.DaysBeforeDeadline's own doc
        // comment) — binding straight onto the 7-item default would silently grow it to 9 items
        // instead of honouring an operator's override. Checking Exists() first and starting from
        // an empty array only when a real override is present keeps both promises: no
        // configuration section present -> the untouched spec default; a configured override ->
        // exactly that override, not the default plus the override appended.
        services.TryAddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection(ThresholdWindowOptions.SectionName);
            var daysBeforeDeadlineSection = section.GetSection(nameof(ThresholdWindowOptions.DaysBeforeDeadline));

            if (!daysBeforeDeadlineSection.Exists())
            {
                return new ThresholdWindowOptions();
            }

            var options = new ThresholdWindowOptions { DaysBeforeDeadline = [] };
            section.Bind(options);
            return options;
        });

        // Scoped, not Singleton: RenewalThresholdScheduler takes IAuditWriter as a constructor
        // dependency, and Contigo.Audit's own AuditWriter is registered Scoped (bound to a Scoped
        // AuditDbContext) — a Singleton registration here would be a captive-dependency violation
        // the moment both modules are wired into the same host under ValidateScopes. Any host that
        // calls this method must also call Contigo.Audit's AddAuditModule for this to resolve —
        // the exact same landmine Contigo.Worker.WorkerServiceCollectionExtensions.AddWorkerHost's
        // own doc comment already flags for Contigo.Documents.Contracts's DocumentUploadService.
        services.AddScoped<RenewalThresholdScheduler>();
        // RenewalPipelineBuilder (task E03/F03/US01/T01) only depends on RenewalEngine + IClock,
        // both already registered above — same Scoped lifetime for the same reason.
        services.AddScoped<RenewalPipelineBuilder>();

        return services;
    }
}
