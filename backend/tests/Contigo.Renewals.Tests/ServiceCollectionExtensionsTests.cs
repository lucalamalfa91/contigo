using Contigo.Renewals.Application;
using Contigo.Renewals.Configuration;
using Contigo.Renewals.Domain;
using Contigo.Renewals.Infrastructure;
using Contigo.Renewals.Tests.TestSupport;
using Contigo.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Renewals.Tests;

/// <summary>
/// Proves every task's own wiring claim for <see cref="ServiceCollectionExtensions.AddRenewalsModule"/>
/// (mirrors <c>Contigo.Chat.Tests.ServiceCollectionExtensionsTests</c>): each artifact this module
/// has produced resolves from a container that only has <c>AddRenewalsModule</c> registered (plus
/// whatever external dependency a given artifact needs — <see cref="IAuditWriter"/>/
/// <see cref="IConfiguration"/>, both of which every real host already supplies) — the shape
/// <c>Contigo.Api.Program</c> / <c>Contigo.Worker.Program</c> actually rely on:
/// <see cref="RenewalEngine"/> (E03/F01/US01/T01), <see cref="RenewalOpportunityGenerator"/>
/// (E03/F01/US01/T02), <see cref="PriorityScoreCalculator"/> plus its
/// <see cref="PriorityScoreWeightsOptions"/> (E03/F01/US02/T01 and, for the options binding,
/// E03/F01/US02/T02), <see cref="Contigo.Renewals.Application.RenewalThresholdScheduler"/> plus its
/// <see cref="ThresholdWindowOptions"/> (E03/F02/US01/T01), and <see cref="RenewalPipelineBuilder"/>
/// (E03/F03/US01/T01).
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
    public void AddRenewalsModule_resolves_RenewalOpportunityGenerator_and_PriorityScoreCalculator_with_no_captive_dependency()
    {
        var services = new ServiceCollection();
        // ValidateOnBuild below eagerly validates every descriptor AddRenewalsModule registers —
        // including RenewalThresholdScheduler's, which needs IAuditWriter/ThresholdWindowOptions'
        // own IConfiguration dependency — not just the two types this test actually resolves. Same
        // "supply what ValidateOnBuild needs to walk the whole graph" requirement
        // AddRenewalsModule_resolves_RenewalEngine_and_RenewalThresholdScheduler_with_no_captive_dependency
        // already documents.
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalOpportunityGenerator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PriorityScoreCalculator>());
    }

    /// <summary>
    /// Task E03/F01/US02/T02 (priority-explainability): mirrors
    /// <see cref="AddRenewalsModule_resolves_RenewalEngine_and_RenewalThresholdScheduler_with_no_captive_dependency"/>
    /// for the new options — no "Renewals:PriorityWeights" configuration section supplied,
    /// <see cref="PriorityScoreWeightsOptions"/>'s own property initializers (the spec-default 20
    /// per component) must still produce a usable, resolvable options singleton.
    /// </summary>
    [Fact]
    public void AddRenewalsModule_resolves_PriorityScoreWeightsOptions_with_the_spec_default_when_unconfigured()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<PriorityScoreWeightsOptions>();

        Assert.Equal(20m, options.SpendWeightMax);
        Assert.Equal(20m, options.TimeUrgencyMax);
        Assert.Equal(20m, options.BenchmarkOpportunityMax);
        Assert.Equal(20m, options.PriceIncreaseRiskMax);
        Assert.Equal(20m, options.ContractRiskMax);
    }

    /// <summary>
    /// Task E03/F01/US02/T02: proves the actual "tunable" wiring end to end — a configured
    /// <c>Renewals:PriorityWeights</c> section overrides the defaults, and the
    /// <see cref="PriorityScoreCalculator"/> the same container resolves actually uses the
    /// overridden weight (not just that the options object itself bound correctly, which the
    /// previous test already proves for the unconfigured case).
    /// </summary>
    [Fact]
    public void AddRenewalsModule_binds_weights_from_the_configured_Renewals_PriorityWeights_section_and_the_resolved_calculator_uses_them()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Renewals:PriorityWeights:SpendWeightMax"] = "40",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<PriorityScoreWeightsOptions>();

        // The configured property is overridden; every other property keeps its spec default —
        // same "config only overlays what is present" promise ThresholdWindowOptions makes.
        Assert.Equal(40m, options.SpendWeightMax);
        Assert.Equal(20m, options.TimeUrgencyMax);
        Assert.Equal(20m, options.BenchmarkOpportunityMax);
        Assert.Equal(20m, options.PriceIncreaseRiskMax);
        Assert.Equal(20m, options.ContractRiskMax);

        using var scope = provider.CreateScope();
        var calculator = scope.ServiceProvider.GetRequiredService<PriorityScoreCalculator>();

        var renewal = new RenewalCalculationResult(
            EntityId.New(), RenewalCalculationStatus.Determined, null, null, null, null, "fixture");
        var result = calculator.Calculate(renewal, new RenewalPriorityInputs(600_000m, null, null, null));

        // 600,000 annual spend is the top spend tier (>= 500,000): with SpendWeightMax=40 the
        // maximum spend-weight contribution is 40, not the spec-default 20 — proof the container
        // wired the *configured* PriorityScoreWeightsOptions into the calculator, not a fallback
        // default one.
        Assert.Equal(40m, result.SpendWeight.Score);
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
        // See AddRenewalsModule_resolves_RenewalOpportunityGenerator_and_PriorityScoreCalculator_with_no_captive_dependency's
        // own comment: ValidateOnBuild below validates every AddRenewalsModule descriptor, so
        // RenewalThresholdScheduler's own dependencies must be satisfiable here too, even though
        // this test never resolves it directly.
        services.AddSingleton<IAuditWriter>(new RecordingAuditWriter());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalPipelineBuilder>());
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
