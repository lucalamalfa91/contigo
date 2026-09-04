using Contigo.Chat.Application;
using Contigo.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Chat.Infrastructure;

/// <summary>
/// Composition-root wiring for the Chat module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a host
/// directly). Task E02/F04/US01/T01 (query-router) and task E02/F04/US01/T02
/// (deterministic-queries) added <see cref="AskContigoQueryRouter"/>,
/// <see cref="DeterministicQueryPlanner"/> and <see cref="DeterministicQueryHandler"/> but no host
/// took a dependency on any of them yet, so this composition method did not exist —
/// <c>Contigo.Api.Contigo.Api.csproj</c> already carried a <c>ProjectReference</c> to
/// <c>Contigo.Chat.csproj</c> in anticipation of it (see that file). Task E02/F04/US02/T01
/// (rag-citations) is the first thing that needs any of this resolvable from a container — it adds
/// <see cref="RagAnswerService"/> alongside the three pre-existing types, and
/// <c>Contigo.Api.ChatEndpointExtensions</c> (<c>POST /api/chat/query</c>) is the first caller. Task
/// E02/F04/US02/T02 (abstain-guard) adds <see cref="AbstainGuard"/>, a constructor dependency of
/// <see cref="RagAnswerService"/> (see that type's own doc comment).
///
/// Every registration is Scoped, not Singleton: <see cref="RagAnswerService"/> depends on
/// <see cref="IAuditWriter"/>, which <c>Contigo.Audit.Infrastructure.ServiceCollectionExtensions
/// .AddAuditModule</c> registers Scoped (it wraps a Scoped <c>AuditDbContext</c>) — a Singleton
/// <see cref="RagAnswerService"/> would capture that Scoped dependency for the lifetime of the
/// host, which <c>ServiceProviderOptions.ValidateOnBuild</c> (enabled by default for the
/// Development environment <c>WebApplicationFactory</c>-based tests run under) rejects at startup.
/// <see cref="AskContigoQueryRouter"/>/<see cref="DeterministicQueryPlanner"/>/
/// <see cref="DeterministicQueryHandler"/>/<see cref="AbstainGuard"/> have no such constraint but
/// are registered the same way for one uniform per-request/job lifetime across the module — the
/// same choice <c>Contigo.Documents.Contracts.Infrastructure.ServiceCollectionExtensions
/// .AddDocumentsContractsModule</c> already makes for every one of its own services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChatModule(this IServiceCollection services)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first registration
        // wins, and every module shares the same "now" (IClock) — mirrors
        // Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule /
        // Contigo.Documents.Contracts.Infrastructure.ServiceCollectionExtensions
        // .AddDocumentsContractsModule.
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddScoped<AskContigoQueryRouter>();
        services.AddScoped<DeterministicQueryPlanner>();
        services.AddScoped<DeterministicQueryHandler>();
        services.AddScoped<AbstainGuard>();
        services.AddScoped<RagAnswerService>();

        return services;
    }
}
