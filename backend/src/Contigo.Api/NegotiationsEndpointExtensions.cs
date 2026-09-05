using Contigo.Quotes.Application.Outcome;
using Contigo.SharedKernel;

namespace Contigo.Api;

/// <summary>
/// Maps `POST /api/negotiations/outcomes` (product spec Appendix A API table: "Record outcome";
/// spec §12.2 "Negotiation outcome capture"; module-map.md "Quotes | Quote, QuoteLine, Assessment,
/// NegotiationOutcome | /api/quotes, /api/negotiations/outcomes"; parent story
/// us-02-outcome-capture AC-1, task E05/F03/US02/T01, negotiation-outcome). Thin composition per
/// ADR-002 — <see cref="NegotiationOutcomeService"/> owns every validation/persistence/audit
/// decision; this file only translates HTTP &lt;-&gt; that call, the same shape
/// <see cref="QuotesEndpointExtensions"/>/<see cref="SavingsEndpointExtensions"/> already use.
///
/// Same interim `X-Tenant-Id` header placeholder as every other endpoint in this host (ADR-010 is
/// not in this task's "Architecture decisions in force" list, so there is no validated caller
/// principal yet) — see <c>Program.cs</c>'s own comment on why this interim gap is not promoted to
/// reports/open-questions.md by these tasks.
/// </summary>
public static class NegotiationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapNegotiationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/negotiations/outcomes", CaptureOutcomeAsync);
        return endpoints;
    }

    /// <summary>
    /// AC-1 ("`POST /api/negotiations/outcomes` records original/target/final/saving/discount/
    /// duration/levers"): a plain JSON body (unlike `POST /api/quotes`'s multipart upload) —
    /// <paramref name="request"/> binds directly from it (minimal API's default complex-type-as-body
    /// inference, the same mechanism <c>SavingsEndpointExtensions.PatchSavingsOpportunityAsync</c>'s
    /// own <c>SavingsOpportunityPatchRequest</c> parameter already relies on). 404 when
    /// <see cref="NegotiationOutcomeService.QuoteNotFoundError"/> comes back (no such quote for this
    /// tenant — same <c>Result&lt;T&gt;.Error</c>-sentinel-to-404 convention
    /// <see cref="SavingsEndpointExtensions"/>'s own `PATCH /api/savings/{id}` handler already uses
    /// for <c>SavingsOpportunityService.NotFoundError</c>), 400 with <see cref="Result{T}.Error"/>
    /// for every other validation failure.
    /// </summary>
    private static async Task<IResult> CaptureOutcomeAsync(
        NegotiationOutcomeCaptureRequest request,
        HttpRequest httpRequest,
        NegotiationOutcomeService outcomeService,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
            || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
        {
            return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
        }

        var result = await outcomeService.CaptureAsync(
            new TenantId(tenantGuid), request, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return string.Equals(result.Error, NegotiationOutcomeService.QuoteNotFoundError, StringComparison.Ordinal)
                ? Results.NotFound(result.Error)
                : Results.BadRequest(result.Error);
        }

        var outcome = result.Value;

        return Results.Created($"/api/negotiations/outcomes/{outcome.Id.Value}", new
        {
            id = outcome.Id.Value,
            quoteId = outcome.QuoteId.Value,
            originalQuoteTotal = outcome.OriginalQuoteTotal,
            targetPrice = outcome.TargetPrice,
            finalPrice = outcome.FinalPrice,
            realizedSaving = outcome.RealizedSaving,
            discountPercent = outcome.DiscountPercent,
            negotiationDurationDays = outcome.NegotiationDurationDays,
            leversUsed = outcome.LeversUsed.Select(l => l.ToString()),
            capturedAt = outcome.CapturedAt,
        });
    }
}
