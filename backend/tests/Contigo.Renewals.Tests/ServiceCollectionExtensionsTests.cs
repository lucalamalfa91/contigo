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
///
/// Task E03/F03/US01/T02 (renewal-action): <see cref="ServiceCollectionExtensions.AddRenewalsModule"/>
/// now takes a connection string (this module's first <c>DbContext</c>) — every call site below
/// passes <see cref="ConnectionString"/>, a syntactically valid but never-connected-to Npgsql
/// string (same "eager `UseNpgsql()` parsing only, no real Postgres required" convention
/// <c>Contigo.Api.Tests.DeployableApiTests</c>/<c>Contigo.Worker.Tests.DeployableWorkerTests</c>
/// already use) — none of these tests exercise <see cref="RenewalActionService"/> itself (see
/// <c>RenewalActionServiceTests</c> for that, against a real Testcontainers Postgres).
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=contigo_dev;Username=contigo;Password=contigo;Include Error Detail=true";

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

        services.AddRenewalsModule(ConnectionString);

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

        services.AddRenewalsModule(ConnectionString);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ThresholdWindowOptions>();

        Assert.Equal([45, 10], options.DaysBeforeDeadline);
    }

    [Fact]
    public void AddRenewalsModule_resolves_RenewalOpportunityGenerator_with_no_captive_dependency()
    {
        var services = new ServiceCollection();
        // AddRenewalsModule also unconditionally registers the Scoped RenewalThresholdScheduler
        // and RenewalActionService, both of which need IAuditWriter (see this class's own doc
        // comment) — ValidateOnBuild below fails the whole container, not just the type under
        // test, without this registration.
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());

        services.AddRenewalsModule(ConnectionString);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalOpportunityGenerator>());
    }

    [Fact]
    public void AddRenewalsModule_resolves_PriorityScoreCalculator_with_no_captive_dependency()
    {
        var services = new ServiceCollection();
        // Same IAuditWriter landmine as the RenewalOpportunityGenerator test above.
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());

        services.AddRenewalsModule(ConnectionString);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PriorityScoreCalculator>());
    }

    /// <summary>
    /// Task E03/F03/US01/T01 (renewal-dashboard): <see cref="RenewalPipelineBuilder"/> must resolve
    /// from the same container — <c>Contigo.Api.RenewalsEndpointExtensions</c> takes it as a
    /// minimal-API handler parameter, which only works if DI can construct it.
    /// </summary>
    [Fact]
    public void AddRenewalsModule_resolves_RenewalPipelineBuilder_with_no_captive_dependency()
    {
        var services = new ServiceCollection();
        // Same IAuditWriter landmine as the RenewalOpportunityGenerator test above.
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());

        services.AddRenewalsModule(ConnectionString);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalPipelineBuilder>());
    }

    /// <summary>
    /// Task E03/F03/US01/T02 (renewal-action): <see cref="RenewalActionService"/> must resolve
    /// from the same container — <c>Contigo.Api.RenewalsEndpointExtensions</c>' new POST handler
    /// takes it as a minimal-API handler parameter, same reason as
    /// <see cref="AddRenewalsModule_resolves_RenewalPipelineBuilder_with_no_captive_dependency"/>
    /// above. Needs <see cref="IAuditWriter"/> registered first (same landmine as
    /// <see cref="RenewalThresholdScheduler"/> above).
    /// </summary>
    [Fact]
    public void AddRenewalsModule_resolves_RenewalActionService_with_no_captive_dependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());

        services.AddRenewalsModule(ConnectionString);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalActionService>());
    }

    [Fact]
    public void AddRenewalsModule_does_not_override_an_already_registered_IClock()
    {
        var services = new ServiceCollection();
        var preRegisteredClock = new FixedClock(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));
        services.AddSingleton<IClock>(preRegisteredClock);

        services.AddRenewalsModule(ConnectionString);

        using var provider = services.BuildServiceProvider();

        // TryAddSingleton: the first registration wins — same defensive convention every other
        // module's own AddXxxModule already uses.
        Assert.Same(preRegisteredClock, provider.GetRequiredService<IClock>());
    }
}
