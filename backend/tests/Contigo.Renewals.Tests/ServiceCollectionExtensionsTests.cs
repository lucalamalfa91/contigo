using Contigo.Renewals.Application;
using Contigo.Renewals.Configuration;
using Contigo.Renewals.Infrastructure;
using Contigo.Renewals.Tests.TestSupport;
using Contigo.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Renewals.Tests;

/// <summary>
/// Proves task E03/F01/US01/T01's own wiring claim (mirrors
/// <c>Contigo.Chat.Tests.ServiceCollectionExtensionsTests</c>): <see cref="RenewalEngine"/> is
/// resolvable from a container that only has <see cref="ServiceCollectionExtensions.AddRenewalsModule"/>
/// registered — no external dependency needed, unlike the Chat module's own equivalent test — the
/// shape a future host wiring (<c>Contigo.Api.Program</c> / <c>Contigo.Worker.Program</c>) will
/// rely on once a task adds the first real caller. Task E03/F01/US01/T02 (renewal-opportunity)
/// extends this same proof to <see cref="RenewalOpportunityGenerator"/>, which depends on
/// <see cref="RenewalEngine"/> — resolving it from the same container proves the constructor
/// dependency is satisfied by this one <c>AddRenewalsModule</c> call, not by some other module's
/// registration.
/// rely on once a task adds the first real caller. Task E03/F01/US02/T01 (priority-score) extends
/// this same proof to <see cref="PriorityScoreCalculator"/>.
/// Proves task E03/F01/US01/T01's and E03/F02/US01/T01's own wiring claims (mirrors
/// <c>Contigo.Chat.Tests.ServiceCollectionExtensionsTests</c>): <see cref="RenewalEngine"/> and
/// <see cref="RenewalThresholdScheduler"/> are resolvable from a container that only has
/// <see cref="ServiceCollectionExtensions.AddRenewalsModule"/> registered (plus the
/// <see cref="IAuditWriter"/>/<see cref="IConfiguration"/> every host already supplies) — the shape
/// <c>Contigo.Worker.WorkerServiceCollectionExtensions.AddWorkerHost</c> relies on now that it
/// calls <see cref="ServiceCollectionExtensions.AddRenewalsModule"/> for real.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRenewalsModule_resolves_RenewalEngine_and_RenewalThresholdScheduler_with_no_captive_dependency()
    {
        var services = new ServiceCollection();
        // RenewalThresholdScheduler needs IAuditWriter (Contigo.Audit's own AddAuditModule job in
        // a real host — see AddRenewalsModule's own doc comment) and ThresholdWindowOptions' bind
        // factory needs IConfiguration (always supplied by WebApplicationBuilder /
        // Host.CreateApplicationBuilder in a real host).
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalEngine>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IClock>());

        // Task E03/F02/US01/T01 (threshold-scheduler): no "AiGateway:Ocr"-style configuration
        // section supplied — ThresholdWindowOptions' own property initializer (product spec §9.1's
        // default ladder) must still produce a usable options object, and the Scoped
        // RenewalThresholdScheduler must resolve without a captive-dependency violation even
        // though it depends on the Scoped IAuditWriter.
        var options = scope.ServiceProvider.GetRequiredService<ThresholdWindowOptions>();
        Assert.Equal([365, 270, 180, 120, 90, 60, 30], options.DaysBeforeDeadline);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalThresholdScheduler>());
    }

    [Fact]
    public void AddRenewalsModule_binds_thresholds_from_the_configured_Renewals_Thresholds_section()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Renewals:Thresholds:DaysBeforeDeadline:0"] = "45",
                ["Renewals:Thresholds:DaysBeforeDeadline:1"] = "10",
            })
            .Build();
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());
        services.AddSingleton<IConfiguration>(configuration);

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ThresholdWindowOptions>();

        Assert.Equal([45, 10], options.DaysBeforeDeadline);
    }

    [Fact]
    public void AddRenewalsModule_resolves_RenewalOpportunityGenerator_with_no_captive_dependency()
    public void AddRenewalsModule_resolves_PriorityScoreCalculator_with_no_captive_dependency()
    {
        var services = new ServiceCollection();

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalOpportunityGenerator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PriorityScoreCalculator>());
    }

    [Fact]
    public void AddRenewalsModule_does_not_override_an_already_registered_IClock()
    {
        var services = new ServiceCollection();
        var preRegisteredClock = new FixedClock(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));
        services.AddSingleton<IClock>(preRegisteredClock);

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider();

        // TryAddSingleton: the first registration wins — same defensive convention every other
        // module's own AddXxxModule already uses.
        Assert.Same(preRegisteredClock, provider.GetRequiredService<IClock>());
    }
}
