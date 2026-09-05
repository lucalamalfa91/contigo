using Contigo.Benchmark.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Benchmark.Tests;

/// <summary>
/// Proves task E04/F01/US02/T01's own wiring claim: <see cref="IBenchmarkService"/>'s doc comment
/// says "story us-02-fixture-adapter adds the first adapter" — this is that registration, mirroring
/// <c>Contigo.AiGateway.Tests.ServiceCollectionExtensionsTests</c>' identical proof for
/// <c>AddAiGatewayModule</c>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBenchmarkModule_resolves_a_fixture_backed_benchmark_service()
    {
        var services = new ServiceCollection();

        services.AddBenchmarkModule();

        using var provider = services.BuildServiceProvider();

        var benchmarkService = provider.GetRequiredService<IBenchmarkService>();

        Assert.IsType<FixtureBenchmarkAdapter>(benchmarkService);
    }
}
