using Contigo.AiGateway;
using Contigo.Documents.Contracts.Application;
using Contigo.Documents.Contracts.Application.Extraction;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Documents.Contracts.Infrastructure;

/// <summary>
/// Composition-root wiring for the Documents/Contracts module. ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"; domain modules never wire themselves into a
/// host directly. Task E01/F04/US03/T01 (us-03) wired the ambient tenant claim
/// (<see cref="ITenantContext"/>) and the RLS connection interceptor into the DbContext pipeline
/// itself, so the first endpoint/handler to land only had to call
/// <see cref="ITenantContext.BeginScope"/> around it — the RLS backstop was already live. That
/// first endpoint is task E01/F06/US01/T01's <c>POST /api/documents</c>, wired via
/// <see cref="DocumentUploadService"/>, registered here alongside the DbContext. Task
/// E01/F06/US01/T02's <c>GET /api/documents/{id}</c> reuses the same DbContext registration and
/// adds <see cref="DocumentQueryService"/> alongside it. Task E02/F01/US02/T01
/// (us-02-staged-extraction) adds <see cref="StagedExtractionService"/>, and with it this
/// module's first real dependency on <c>Contigo.AiGateway</c> (already allow-listed for this
/// module — <c>Contigo.ArchitectureTests.DependencyDirectionTests</c>) — see
/// <see cref="Contigo.AiGateway.ServiceCollectionExtensions.AddAiGatewayModule"/>'s own doc
/// comment for why calling it from here, rather than adding it to every host's own composition
/// (<c>Contigo.Api/Program.cs</c>, <c>Contigo.Worker.WorkerServiceCollectionExtensions</c>),
/// keeps <see cref="IAiGateway"/> resolvable everywhere this module already is without changing
/// either host's code.
/// adds <see cref="DocumentQueryService"/> alongside it. Task E02/F03/US01/T01's
/// <c>GET /api/contracts</c> reuses it again and adds <see cref="PortfolioQueryService"/>.
/// adds <see cref="DocumentQueryService"/> alongside it. Task E02/F05/US01/T01's `PATCH
/// /api/contracts/{id}` (<see cref="ContractCorrectionService"/>) reuses the same registration
/// again. Task E02/F02/US02/T02 (us-02-embedding-search-index) adds
/// <see cref="EmbeddingRetrievalService"/> alongside it — no new dependency to wire, since the
/// module's own <see cref="IAiGateway"/> registration (this method's own
/// <c>AddAiGatewayModule</c> call, above) already resolves everything that service needs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsContractsModule(
        this IServiceCollection services, string connectionString)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first
        // registration wins, and every module shares the same ambient tenant claim (ADR-009)
        // and the same "now" (IClock).
        services.TryAddSingleton<ITenantContext, TenantContext>();
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddDbContext<DocumentsContractsDbContext>(
            (sp, options) => DocumentsContractsDbContextOptions.Configure(
                options, connectionString, sp.GetRequiredService<ITenantContext>()));

        // See the type doc comment: this module's own IAiGateway/AiGatewayModelOptions wiring.
        services.AddAiGatewayModule();

        // Scoped: shares the request/job's own DbContext instance (also Scoped, via AddDbContext
        // above) rather than a second, independently-tracked context.
        services.AddScoped<DocumentUploadService>();
        services.AddScoped<DocumentQueryService>();
        services.AddScoped<StagedExtractionService>();
        services.AddScoped<PortfolioQueryService>();
        services.AddScoped<ContractCorrectionService>();
        services.AddScoped<EmbeddingRetrievalService>();

        return services;
    }
}
