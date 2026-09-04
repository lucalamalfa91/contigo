using Contigo.AiGateway.Configuration;
using Contigo.AiGateway.Fixtures;
using Contigo.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.AiGateway;

/// <summary>
/// Composition-root wiring for the AI Gateway module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"). <see cref="AiGatewayModelOptions"/>'s own doc
/// comment names task E02/F01/US02/T01 (staged extraction) as "the first caller" that needed
/// <see cref="IAiGateway"/> resolvable from DI — nothing before this task constructed the
/// gateway via a container. Called from
/// <see cref="Contigo.Documents.Contracts.Infrastructure.ServiceCollectionExtensions.AddDocumentsContractsModule"/>
/// (that module is the first, and today only, consumer — its own allow-listed reference to
/// <c>Contigo.AiGateway</c> is exactly for this), so every host that already calls
/// <c>AddDocumentsContractsModule</c> (Api, Worker) gets a working <see cref="IAiGateway"/>
/// automatically, with no call-site signature change and no risk of the
/// "<c>ServiceProviderOptions.ValidateOnBuild</c> throws because a dependency was registered
/// without its own dependency" landmine <c>WorkerServiceCollectionExtensions.AddWorkerHost</c>'s
/// own doc comment already flags for the identical <c>IAuditWriter</c> situation.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiGatewayModule(this IServiceCollection services)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first
        // registration wins (mirrors Contigo.Documents.Contracts.Infrastructure.ServiceCollectionExtensions).
        services.TryAddSingleton<IClock, SystemClock>();

        // Bound lazily inside the factory (not via the Options pattern — FixtureAiGateway's
        // constructor takes the plain AiGatewayModelOptions type, so there is nothing an
        // IOptions<T> indirection would buy here) by resolving IConfiguration from the
        // container: it is always already registered by WebApplicationBuilder /
        // Host.CreateApplicationBuilder, so this method does not need its own IConfiguration
        // parameter threaded through every caller (Contigo.Api/Program.cs,
        // Contigo.Worker/WorkerServiceCollectionExtensions, AddDocumentsContractsModule).
        // AiGatewayModelOptions's own property initializers already supply ADR-004's candidate
        // defaults, so a deployment with no "AiGateway:Models" section configured still gets a
        // working (fixture-backed) gateway — Bind only overlays keys that are actually present.
        services.TryAddSingleton(sp =>
        {
            var options = new AiGatewayModelOptions();
            sp.GetRequiredService<IConfiguration>()
                .GetSection(AiGatewayModelOptions.SectionName)
                .Bind(options);
            return options;
        });

        // ADR-004 "Implications for the decomposition" / ADR-017: "until [a live Foundry
        // endpoint exists], a fixture gateway adapter satisfies R0 scaffolding" — no
        // infra/modules Terraform module or Foundry connection string exists yet (see
        // FixtureAiGateway's own doc comment), so this is the only real IAiGateway
        // implementation to register today. Swapping in a live Foundry-backed implementation
        // later is a one-line change here; domain code depends on IAiGateway, never this type.
        services.TryAddSingleton<IAiGateway, FixtureAiGateway>();

        return services;
    }
}
