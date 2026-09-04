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
/// directly). Task E03/F01/US01/T01 (deterministic-dates) added <see cref="RenewalEngine"/>; task
/// E03/F02/US01/T01 (threshold-scheduler) adds <see cref="RenewalThresholdScheduler"/> and its
/// <see cref="ThresholdWindowOptions"/> — the first real callers named by this method's own
/// original doc comment ("the three tasks that depend on renewal-engine: renewal-opportunity
/// generation, priority score, the threshold scheduler"). No host endpoint calls
/// <see cref="RenewalEngine"/> directly yet, but <c>Contigo.Worker.WorkerServiceCollectionExtensions
/// .AddWorkerHost</c> now calls <see cref="AddRenewalsModule"/> so its own daily-cadence hosted
/// service can resolve <see cref="RenewalThresholdScheduler"/> — the same "wiring lands with the
/// first real caller" sequencing <c>Contigo.Chat.Infrastructure.ServiceCollectionExtensions</c>'s
/// own doc comment describes for that module.
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

        return services;
    }
}
