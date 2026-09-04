using System.Globalization;
using Contigo.Documents.Contracts.Application;
using Contigo.Documents.Contracts.Domain;
using Contigo.SharedKernel;

namespace Contigo.Api;

/// <summary>
/// Maps `GET /api/contracts` (product spec API table row "Portfolio list/filter"; story
/// us-01-portfolio-list-filters AC-1/AC-2/AC-3, task E02/F03/US01/T01). Thin composition per
/// ADR-002 — the actual decisions are made by <see cref="PortfolioQueryService"/>; this file only
/// parses the query string into a <see cref="PortfolioFilter"/> and maps each outcome to an HTTP
/// status code.
///
/// Same interim-authentication placeholder as <c>Program</c>'s document endpoints: ADR-010 is not
/// in this task's "architecture decisions in force" list, so there is still no validated caller
/// principal to take the tenant from. The tenant is taken from an explicit <c>X-Tenant-Id</c>
/// header instead of a token claim — see <c>Program.cs</c>'s own comment on why this gap is not
/// promoted to reports/open-questions.md by this task (a mid-wave append there has previously
/// broken a phase-barrier merge).
/// </summary>
public static class PortfolioEndpointExtensions
{
    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/contracts", GetPortfolioAsync);
        return endpoints;
    }

    private static async Task<IResult> GetPortfolioAsync(
        HttpRequest request,
        PortfolioQueryService portfolioQueryService,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
            || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
        {
            return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
        }

        if (!TryParseFilter(request.Query, out var filter, out var error))
        {
            return Results.BadRequest(error);
        }

        var items = await portfolioQueryService
            .GetPortfolioAsync(new TenantId(tenantGuid), filter, cancellationToken)
            .ConfigureAwait(false);

        // Enum members are projected to their string names for the wire contract — the same
        // convention Program.cs already uses for DocumentType/ProcessingStatus on
        // GET /api/documents/{id} — rather than the JSON serializer's numeric default.
        return Results.Ok(items.Select(item => new
        {
            contractId = item.ContractId,
            supplierId = item.SupplierId,
            type = item.Type.ToString(),
            annualSpend = item.AnnualSpend,
            startDate = item.StartDate,
            endDate = item.EndDate,
            renewalDate = item.RenewalDate,
            cancellationDeadline = item.CancellationDeadline,
            autoRenewal = item.AutoRenewal,
            status = item.Status,
            risk = item.Risk?.ToString(),
        }));
    }

    /// <summary>
    /// Parses the AC-2 filter query parameters (supplierId, status, risk, autoRenewal,
    /// minAnnualSpend, maxAnnualSpend, renewalFrom, renewalTo). No "category" parameter exists —
    /// see <see cref="PortfolioFilter"/>'s own doc comment for why. Every parameter is optional;
    /// an absent one leaves the corresponding <see cref="PortfolioFilter"/> member null (not
    /// filtered). Returns false with a caller-facing <paramref name="error"/> on the first
    /// malformed value found.
    /// </summary>
    private static bool TryParseFilter(
        IQueryCollection query, out PortfolioFilter filter, out string error)
    {
        filter = PortfolioFilter.None;
        error = string.Empty;

        EntityId? supplierId = null;
        if (query.TryGetValue("supplierId", out var supplierIdValues))
        {
            if (!Guid.TryParse(supplierIdValues.ToString(), out var supplierGuid))
            {
                error = "'supplierId' must be a GUID.";
                return false;
            }

            supplierId = new EntityId(supplierGuid);
        }

        string? status = query.TryGetValue("status", out var statusValues)
            ? statusValues.ToString()
            : null;

        RiskSeverity? risk = null;
        if (query.TryGetValue("risk", out var riskValues))
        {
            if (!Enum.TryParse<RiskSeverity>(riskValues.ToString(), ignoreCase: true, out var parsedRisk))
            {
                error = "'risk' must be one of Low, Medium, High, Critical.";
                return false;
            }

            risk = parsedRisk;
        }

        bool? autoRenewal = null;
        if (query.TryGetValue("autoRenewal", out var autoRenewalValues))
        {
            if (!bool.TryParse(autoRenewalValues.ToString(), out var parsedAutoRenewal))
            {
                error = "'autoRenewal' must be 'true' or 'false'.";
                return false;
            }

            autoRenewal = parsedAutoRenewal;
        }

        if (!TryParseOptionalDecimal(query, "minAnnualSpend", out var minAnnualSpend, out error)
            || !TryParseOptionalDecimal(query, "maxAnnualSpend", out var maxAnnualSpend, out error))
        {
            return false;
        }

        if (!TryParseOptionalDate(query, "renewalFrom", out var renewalFrom, out error)
            || !TryParseOptionalDate(query, "renewalTo", out var renewalTo, out error))
        {
            return false;
        }

        filter = new PortfolioFilter(
            supplierId, status, risk, autoRenewal, minAnnualSpend, maxAnnualSpend, renewalFrom, renewalTo);
        return true;
    }

    private static bool TryParseOptionalDecimal(
        IQueryCollection query, string name, out decimal? value, out string error)
    {
        value = null;
        error = string.Empty;

        if (!query.TryGetValue(name, out var values))
        {
            return true;
        }

        if (!decimal.TryParse(values.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"'{name}' must be a number.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryParseOptionalDate(
        IQueryCollection query, string name, out DateOnly? value, out string error)
    {
        value = null;
        error = string.Empty;

        if (!query.TryGetValue(name, out var values))
        {
            return true;
        }

        if (!DateOnly.TryParse(values.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = $"'{name}' must be a date (yyyy-MM-dd).";
            return false;
        }

        value = parsed;
        return true;
    }
}
