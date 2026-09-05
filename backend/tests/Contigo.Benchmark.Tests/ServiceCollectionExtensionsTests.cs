using Contigo.Benchmark.Configuration;
using Contigo.Benchmark.Contracts;
using Contigo.Benchmark.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Benchmark.Tests;

/// <summary>
/// Proves task E04/F01/US01/T02's own wiring claim: <see cref="BenchmarkAdapterOptions"/> and
/// <see cref="IBenchmarkService"/> both resolve from a DI container via
/// <see cref="ServiceCollectionExtensions.AddBenchmarkModule"/>, mirroring
/// <c>Contigo.AiGateway.Tests.ServiceCollectionExtensionsTests</c>. Task E04/F01/US02/T02
/// (fixture-confidence) extends this with proof that the default configuration now resolves a
/// genuinely working, fixture-backed <see cref="IBenchmarkService"/> — completing the registration
/// <see cref="IBenchmarkService"/>'s own doc comment says story us-02-fixture-adapter would add.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBenchmarkModule_resolves_a_registry_backed_service_with_the_default_adapter_name()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddBenchmarkModule();

        using var provider = services.BuildServiceProvider();

        var benchmarkService = provider.GetRequiredService<IBenchmarkService>();
        Assert.IsType<BenchmarkAdapterRegistry>(benchmarkService);

        // No "Benchmark:Adapter" configuration section supplied — BenchmarkAdapterOptions's own
        // property initializer must still produce a usable options object.
        var options = provider.GetRequiredService<BenchmarkAdapterOptions>();
        Assert.Equal(BenchmarkAdapterOptions.DefaultAdapterName, options.ActiveAdapter);
    }

    /// <summary>
    /// Task E04/F01/US02/T02: proves the registry is no longer merely "resolvable" but actually
    /// dispatches to a real adapter under the default configuration — <see
    /// cref="FixtureBenchmarkAdapter"/> is now registered as the <c>"fixture"</c>-named
    /// <c>IBenchmarkProviderAdapter</c> <see cref="ServiceCollectionExtensions.AddBenchmarkModule"/>
    /// wires in, matching <see cref="BenchmarkAdapterOptions.DefaultAdapterName"/>.
    /// </summary>
    [Fact]
    public async Task AddBenchmarkModule_resolves_a_working_fixture_backed_benchmark_service()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddBenchmarkModule();
        using var provider = services.BuildServiceProvider();

        var benchmarkService = provider.GetRequiredService<IBenchmarkService>();
        var result = await benchmarkService.GetBenchmarkAsync(new BenchmarkQuery(
            "AWS", "EC2 Compute", "m5.large", "US", 50m, "12 months", "USD", new DateOnly(2026, 8, 1)));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.HasSufficientData);
        Assert.Equal("fixture", result.Value.Source);
        Assert.Equal(new BenchmarkDistribution(0.085m, 0.096m, 0.108m), result.Value.Distribution);
    }

    [Fact]
    public void AddBenchmarkModule_binds_the_active_adapter_from_the_configured_section()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Benchmark:Adapter:ActiveAdapter"] = "paid-provider",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddBenchmarkModule();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<BenchmarkAdapterOptions>();

        Assert.Equal("paid-provider", options.ActiveAdapter);
    }

    /// <summary>
    /// Task E04/F01/US02/T02: the only adapter this module ever registers is named
    /// <c>"fixture"</c> (<see cref="FixtureBenchmarkAdapter.Name"/>); configuring a different
    /// <see cref="BenchmarkAdapterOptions.ActiveAdapter"/> name — the shape a later,
    /// council-justified paid-provider adapter would eventually use — must still fail honestly
    /// rather than silently falling back to the fixture or throwing at startup (ADR-001).
    /// </summary>
    [Fact]
    public async Task Resolved_service_fails_honestly_when_the_configured_adapter_name_is_not_registered()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Benchmark:Adapter:ActiveAdapter"] = "paid-provider",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddBenchmarkModule();
        using var provider = services.BuildServiceProvider();

        var benchmarkService = provider.GetRequiredService<IBenchmarkService>();
        Assert.IsType<BenchmarkAdapterRegistry>(benchmarkService);

        var result = await benchmarkService.GetBenchmarkAsync(new BenchmarkQuery(
            "AWS", "EC2 Compute", null, "US", 100m, "12 months", "USD", new DateOnly(2026, 1, 15)));

        Assert.True(result.IsFailure);
        Assert.Contains("paid-provider", result.Error, StringComparison.Ordinal);
    }
}
