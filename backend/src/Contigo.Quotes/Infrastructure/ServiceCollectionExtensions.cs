using Contigo.Quotes.Application;
using Contigo.Quotes.Application.Extraction;
using Contigo.Quotes.Application.Normalization;
using Contigo.SharedKernel;
using Contigo.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Contigo.Quotes.Infrastructure;

/// <summary>
/// Composition-root wiring for the Quotes module (ADR-002: "each module exposes an
/// AddXxx(IServiceCollection) extension method"). Task E05/F01/US01/T01 (quote-extraction) gives
/// this module its first <c>DbContext</c> (<see cref="QuotesDbContext"/>, backing
/// <see cref="QuoteUploadService"/> and <see cref="QuoteLineExtractionService"/>) — mirrors
/// <c>Contigo.Savings.Infrastructure.ServiceCollectionExtensions.AddSavingsModule</c>'s own shape.
/// Task E05/F01/US01/T02 (quote-normalization) later adds
/// <see cref="QuoteLineNormalizationService"/> to this same registration, sharing this module's one
/// <c>DbContext</c> too.
///
/// Deliberately does <b>not</b> call <c>Contigo.Benchmark.ServiceCollectionExtensions
/// .AddBenchmarkModule</c> — unlike <c>Contigo.Savings</c>/<c>Contigo.Renewals</c>, nothing this
/// task adds resolves <c>IBenchmarkService</c> yet (this task's own coding objective is "Quote
/// upload + line-item extraction", not benchmark matching/assessment — spec §11's own Quote →
/// Benchmark → Assessment flow treats those as later steps). <c>Contigo.Quotes.csproj</c>'s own
/// <c>ProjectReference</c> to <c>Contigo.Benchmark</c> pre-dates this task (an R4 scaffold
/// anticipating that later step); wiring its DI registration ahead of a real caller would be
/// exactly the "invented ahead of need" this codebase's own conventions avoid (see
/// <c>Contigo.Quotes.Domain.Quote</c>'s own doc comment for the same restraint applied to columns).
///
/// Any host that calls this method must also call <c>Contigo.Audit</c>'s <c>AddAuditModule</c> for
/// <see cref="QuoteUploadService"/>'s own <see cref="IAuditWriter"/> dependency to resolve — the
/// same landmine <c>Contigo.Savings.Infrastructure.ServiceCollectionExtensions.AddSavingsModule</c>'s
/// own doc comment flags for <c>SavingsOpportunityService</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuotesModule(this IServiceCollection services, string connectionString)
    {
        // TryAdd: any module (or the host) may call this defensively; only the first registration
        // wins, and every module shares the same "now" (IClock) and the same ambient tenant claim
        // (ADR-009) — mirrors every other module's own AddXxxModule.
        services.TryAddSingleton<IClock, SystemClock>();
        services.TryAddSingleton<ITenantContext, TenantContext>();

        services.AddDbContext<QuotesDbContext>(
            (sp, options) => QuotesDbContextOptions.Configure(
                options, connectionString, sp.GetRequiredService<ITenantContext>()));

        // Scoped: shares the request's own QuotesDbContext instance (also Scoped, via AddDbContext
        // above) rather than a second, independently-tracked context.
        services.AddScoped<QuoteUploadService>();
        services.AddScoped<QuoteLineExtractionService>();
        // Task E05/F01/US01/T02 (quote-normalization): shares this request's own QuotesDbContext
        // (also Scoped, registered above) with QuoteLineExtractionService — see
        // QuoteLineNormalizationService's own doc comment for why that matters.
        services.AddScoped<QuoteLineNormalizationService>();

        return services;
    }
}
