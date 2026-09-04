using Contigo.AiGateway.Configuration;
using Contigo.AiGateway.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.AiGateway.Tests;

/// <summary>
/// Proves task E02/F01/US02/T01's own wiring claim: <see cref="AiGatewayModelOptions"/>'s doc
/// comment names this task as "the first caller" that needed <see cref="IAiGateway"/> resolvable
/// from a DI container, via <see cref="ServiceCollectionExtensions.AddAiGatewayModule"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAiGatewayModule_resolves_a_fixture_backed_gateway_with_ADR_004_defaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddAiGatewayModule();

        using var provider = services.BuildServiceProvider();

        // ADR-004 "Implications for the decomposition": fixture adapter until a live Foundry
        // endpoint exists.
        var gateway = provider.GetRequiredService<IAiGateway>();
        Assert.IsType<FixtureAiGateway>(gateway);

        // No "AiGateway:Models" configuration section supplied — AiGatewayModelOptions's own
        // property initializers (ADR-004 candidates) must still produce a usable options object.
        var options = provider.GetRequiredService<AiGatewayModelOptions>();
        Assert.Equal("gpt-4o-mini", options.Extract.ModelId);
        Assert.Equal("text-embedding-3-small", options.Embed.ModelId);
    }

    [Fact]
    public void AddAiGatewayModule_binds_model_ids_from_the_configured_AiGateway_Models_section()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiGateway:Models:Extract:ModelId"] = "custom-extract-model",
                ["AiGateway:Models:Extract:ModelVersion"] = "42",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddAiGatewayModule();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<AiGatewayModelOptions>();

        Assert.Equal("custom-extract-model", options.Extract.ModelId);
        Assert.Equal("42", options.Extract.ModelVersion);

        // Unconfigured roles keep their ADR-004 default — Bind only overlays present keys.
        Assert.Equal("gpt-4o-mini", options.Classify.ModelId);
    }
}
