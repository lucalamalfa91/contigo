using Contigo.Benchmark.Configuration;
using Microsoft.Extensions.Configuration;
using Contigo.Benchmark.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Benchmark;

/// <summary>
/// Composition-root wiring for the Benchmark Service module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method" — mirrors
/// <c>Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule</c>). Task E04/F01/US01/T02
/// ("adapter registry") is the first caller that needs <see cref="IBenchmarkService"/> resolvable
/// from a DI container — task E04/F01/US01/T01 defined the interface with no implementation or
/// registration at all.
///
/// Registers <see cref="BenchmarkAdapterRegistry"/> as <see cref="IBenchmarkService"/> and binds
/// <see cref="BenchmarkAdapterOptions"/> from configuration — but registers no concrete
/// <c>IBenchmarkProviderAdapter</c>. None exists in this solution yet: story us-02-fixture-adapter's
/// own task runs in the same wave-spec phase as this one (parallel, not sequential — neither
/// depends on the other), so it could not have been wired in here even if this task's scope
/// included it. A host that calls <see cref="AddBenchmarkModule"/> today gets a real, resolvable
/// <see cref="IBenchmarkService"/> whose <c>GetBenchmarkAsync</c> honestly fails every call until a
/// module (this one, or a later caller) also registers an <c>IBenchmarkProviderAdapter</c> — the
/// same "resolvable but answers honestly, never fabricates" contract
/// <see cref="BenchmarkAdapterRegistry"/>'s own doc comment describes.
/// Composition-root wiring for the Benchmark module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"). <see cref="IBenchmarkService"/>'s own doc comment
/// (task E04/F01/US01/T01) says: "Until an adapter is registered, this interface has no
/// implementation or DI registration in the solution — expected for this task's scope... story
/// us-02-fixture-adapter adds the first adapter." This is that registration, added by this task
/// (E04/F01/US02/T01) — mirrors <c>Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule</c>'s
/// identical register-the-only-real-implementation-directly shape.
///
/// No host calls this yet: <c>Contigo.Savings</c> — the first intended consumer per
/// <c>Contigo.ArchitectureTests.DependencyDirectionTests.AllowedReferences</c>' allowed-reference
/// map (Renewals, Savings and Quotes may all reference <see cref="Contigo.Benchmark"/>) — has no
/// source of its own yet in this wave. Same "wiring lands with the first real caller" sequencing
/// <c>Contigo.Chat.Infrastructure.ServiceCollectionExtensions</c>'s own doc comment describes for
/// <c>AddChatModule</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBenchmarkModule(this IServiceCollection services)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first registration
        // wins (mirrors Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule).
        services.TryAddSingleton(sp =>
        {
            var options = new BenchmarkAdapterOptions();
            sp.GetRequiredService<IConfiguration>()
                .GetSection(BenchmarkAdapterOptions.SectionName)
                .Bind(options);
            return options;
        });

        // IEnumerable<IBenchmarkProviderAdapter> resolves to an empty sequence when nothing has
        // registered one yet (standard Microsoft.Extensions.DependencyInjection behaviour for an
        // unregistered IEnumerable<T> — never throws), so BenchmarkAdapterRegistry itself always
        // constructs successfully even before any adapter exists.
        services.TryAddSingleton<IBenchmarkService, BenchmarkAdapterRegistry>();
        //
        // ADR-001: the fixture adapter is the only Benchmark Service implementation for the first
        // `demo` — never a paid external API. Swapping in a later, council-justified provider-backed
        // adapter is a change to this one registration; domain code depends on IBenchmarkService,
        // never this type (us-01-benchmark-interface AC-2).
        services.TryAddSingleton<IBenchmarkService, FixtureBenchmarkAdapter>();

        return services;
    }
}
