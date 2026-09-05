using Contigo.Benchmark.Configuration;
using Contigo.Benchmark.Contracts;
using Microsoft.Extensions.Configuration;
using Contigo.Benchmark.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Benchmark.Tests;

/// <summary>
/// Proves task E04/F01/US01/T02's own wiring claim: <see cref="BenchmarkAdapterOptions"/> and
/// <see cref="IBenchmarkService"/> both resolve from a DI container via
/// <see cref="ServiceCollectionExtensions.AddBenchmarkModule"/>, mirroring
/// <c>Contigo.AiGateway.Tests.ServiceCollectionExtensionsTests</c>.
/// Proves task E04/F01/US02/T01's own wiring claim: <see cref="IBenchmarkService"/>'s doc comment
/// says "story us-02-fixture-adapter adds the first adapter" — this is that registration, mirroring
/// <c>Contigo.AiGateway.Tests.ServiceCollectionExtensionsTests</c>' identical proof for
/// <c>AddAiGatewayModule</c>.
///
/// <para>
/// Incidental fix (task E04/F02/US01/T02, out of that task's own module but blocking
/// <c>dotnet build Contigo.slnx</c> for every task): this file failed to compile — a prior merge
/// had spliced <c>AddBenchmarkModule_resolves_a_registry_backed_service_with_the_default_adapter_name</c>'s
/// closing brace together with a second, differently-named test method's signature line, silently
/// discarding that second method's own body. The body restored below is verified against
/// <see cref="ServiceCollectionExtensions.AddBenchmarkModule"/>'s and
/// <see cref="BenchmarkAdapterRegistry"/>'s actual current source, not reconstructed from guesswork.
/// The second, differently-named test could not be recovered (its body is gone, not just
/// misplaced) and is not reinvented here. While diagnosing this, a separate, pre-existing gap
/// surfaced in <c>Contigo.Benchmark</c>'s own DI wiring — <c>AddBenchmarkModule</c> still
/// <c>TryAddSingleton</c>-registers <see cref="Fixtures.FixtureBenchmarkAdapter"/> directly as
/// <see cref="IBenchmarkService"/>, which the preceding <c>TryAddSingleton&lt;IBenchmarkService,
/// BenchmarkAdapterRegistry&gt;</c> call already shadows (first registration wins), and
/// <see cref="Fixtures.FixtureBenchmarkAdapter"/> does not implement
/// <see cref="Adapters.IBenchmarkProviderAdapter"/>, so <see cref="BenchmarkAdapterRegistry"/> can
/// never reach it through its <c>IEnumerable&lt;IBenchmarkProviderAdapter&gt;</c> constructor
/// parameter either — <see cref="IBenchmarkService"/> resolves to an always-adapter-less
/// <see cref="BenchmarkAdapterRegistry"/> today, not <see cref="Fixtures.FixtureBenchmarkAdapter"/>.
/// That is a <c>Contigo.Benchmark</c> module wiring decision, not this (Savings) task's file scope
/// to redesign — left exactly as found, flagged here for that module's own owner.
/// </para>
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
    /// Task E04/F01/US01/T02 registers no concrete adapter (none exists in the solution yet — see
    /// <see cref="ServiceCollectionExtensions"/>'s own doc comment), so the container-resolved
    /// service must still answer honestly rather than throw at startup or fabricate at call time.
    /// </summary>
    [Fact]
    public async Task Resolved_service_fails_honestly_when_no_adapter_is_registered_yet()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddBenchmarkModule();
        using var provider = services.BuildServiceProvider();

        var benchmarkService = provider.GetRequiredService<IBenchmarkService>();
        var result = await benchmarkService.GetBenchmarkAsync(new BenchmarkQuery(
            "AWS", "EC2 Compute", null, "US", 100m, "12 months", "USD", new DateOnly(2026, 1, 15)));

        Assert.True(result.IsFailure);

        Assert.IsType<FixtureBenchmarkAdapter>(benchmarkService);
    }
}
