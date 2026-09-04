using Contigo.Documents.Contracts.Application;
using Contigo.Renewals.Application;
using Contigo.SharedKernel;

namespace Contigo.Api;

/// <summary>
/// Maps `GET /api/renewals` (product spec Appendix A "Renewal pipeline"; §9.1/§9.3/§10.1; story
/// us-01-renewal-dashboard-api AC-1/AC-2, task E03/F03/US01/T01). Thin composition per ADR-002:
/// <see cref="PortfolioQueryService"/> (Documents/Contracts) already reads the tenant-scoped
/// contract facts an "actionable renewal pipeline" needs (supplier id, annual spend, end date,
/// auto-renewal, the already-extracted cancellation-deadline fact — same rows `GET /api/contracts`
/// returns); <see cref="RenewalPipelineBuilder"/> (Renewals) turns those into a pipeline row plus a
/// facts/recommendations insight card via the deterministic <c>RenewalEngine</c> (task
/// E03/F01/US01/T01, the `renewal-engine` wave-spec artifact this task depends on). Neither module
/// may reference the other (`Contigo.ArchitectureTests.DependencyDirectionTests`'s allow-list for
/// both is exactly `[SharedKernel, AiGateway|Benchmark]`), so mapping <see cref="PortfolioListItem"/>
/// into <see cref="RenewalDashboardCandidate"/> can only happen here, in `Contigo.Api` — "the one
/// project allowed to reference every module" (`backend/README.md` "Dependency direction") — the
/// same pattern <c>ChatEndpointExtensions</c> already uses for <c>EmbeddingSearchResult</c> -&gt;
/// <c>AiEvidenceSnippet</c>.
///
/// Same interim `X-Tenant-Id` header placeholder as every other endpoint in this host
/// (<c>Program.cs</c>'s document endpoints, <see cref="WorkspaceEndpointExtensions"/>,
/// <see cref="PortfolioEndpointExtensions"/>, <see cref="ContractsEndpointExtensions"/>,
/// <see cref="ChatEndpointExtensions"/>): ADR-010 (Entra ID/OIDC) is not in this task's
/// "Architecture decisions in force" list, so there is no validated caller principal yet — see
/// <c>Program.cs</c>'s own comment on why this interim gap is not promoted to
/// reports/open-questions.md by this task.
///
/// Only auto-renewing contracts have a renewal to act on at all (the same "Renewal" derivation
/// rule <see cref="PortfolioListItem.RenewalDate"/>'s own doc comment already states), so the
/// underlying <see cref="PortfolioQueryService.GetPortfolioAsync"/> call below is filtered to
/// <c>AutoRenewal: true</c> — pushed to SQL rather than fetched then discarded. That call reuses
/// <see cref="PortfolioQueryService"/>'s own page-size cap
/// (<see cref="PortfolioPageRequest.MaxPageSize"/> = 100) instead of a dedicated unpaged query: an
/// interim limitation, honestly surfaced via the response's <c>totalCount</c> (a tenant with more
/// than 100 auto-renewing contracts sees only the 100 most recently created ones, and
/// <c>totalCount</c> lets a caller detect that rather than silently trusting an incomplete list —
/// Appendix C rule 10). A dedicated unpaged renewal-candidates query is a follow-up, not attempted
/// by this task.
/// </summary>
public static class RenewalsEndpointExtensions
{
    public static IEndpointRouteBuilder MapRenewalsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/renewals", GetRenewalsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetRenewalsAsync(
        HttpRequest request,
        PortfolioQueryService portfolioQueryService,
        RenewalPipelineBuilder pipelineBuilder,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
            || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
        {
            return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
        }

        var tenantId = new TenantId(tenantGuid);

        var portfolioPage = await portfolioQueryService.GetPortfolioAsync(
            tenantId,
            new PortfolioFilter(AutoRenewal: true),
            new PortfolioPageRequest(Page: 1, PageSize: PortfolioPageRequest.MaxPageSize),
            cancellationToken).ConfigureAwait(false);

        var candidates = portfolioPage.Items.Select(ToCandidate);
        var pipeline = pipelineBuilder.Build(candidates);

        return Results.Ok(new
        {
            items = pipeline.Select(ToPipelineResponse),
            totalCount = portfolioPage.TotalCount,
        });
    }

    /// <summary>
    /// Maps one tenant-scoped portfolio row to the Renewals module's own input shape — the one
    /// mapping only this composition root can do (see the type doc comment). 1:1 field copy; no
    /// decision is made here.
    /// </summary>
    private static RenewalDashboardCandidate ToCandidate(PortfolioListItem item) =>
        new(
            new EntityId(item.ContractId),
            item.SupplierId is { } supplierId ? new EntityId(supplierId) : null,
            item.EndDate,
            item.AutoRenewal,
            item.AnnualSpend,
            item.CancellationDeadline);

    /// <summary>
    /// Wire-shapes <see cref="RenewalPipelineItem"/> per AC-1 (top-level supplier/renewal/days/
    /// spend/deadline/action columns) and AC-2 (nested <c>insightCard.facts</c> /
    /// <c>insightCard.recommendations</c>, spec §9.3). Enum members and <c>EntityId</c>/
    /// <c>EntityId?</c> wrapper values are projected to plain strings/GUIDs — the same convention
    /// <see cref="PortfolioEndpointExtensions"/> and <see cref="ContractsEndpointExtensions"/>
    /// already use.
    /// </summary>
    private static object ToPipelineResponse(RenewalPipelineItem item)
    {
        var facts = item.InsightCard.Facts;
        var recommendations = item.InsightCard.Recommendations;

        return new
        {
            contractId = item.ContractId.Value,
            supplierId = item.SupplierId?.Value,
            status = item.Status.ToString(),
            renewalDate = item.RenewalDate,
            daysUntilRenewal = item.DaysUntilRenewal,
            annualSpend = item.AnnualSpend,
            cancellationDeadline = item.CancellationDeadline,
            daysUntilCancellationDeadline = item.DaysUntilCancellationDeadline,
            autoRenewal = item.AutoRenewal,
            action = recommendations.RecommendedAction,
            insightCard = new
            {
                facts = new
                {
                    supplierId = facts.SupplierId?.Value,
                    renewalDate = facts.RenewalDate,
                    daysUntilRenewal = facts.DaysUntilRenewal,
                    annualSpend = facts.AnnualSpend,
                    cancellationDeadline = facts.CancellationDeadline,
                    daysUntilCancellationDeadline = facts.DaysUntilCancellationDeadline,
                },
                recommendations = new
                {
                    recommendedAction = recommendations.RecommendedAction,
                    explanation = recommendations.Explanation,
                    annualUpliftPercent = recommendations.AnnualUpliftPercent,
                    marketPosition = recommendations.MarketPosition,
                    potentialSavingsRange = recommendations.PotentialSavingsRange,
                },
            },
        };
    }
}
