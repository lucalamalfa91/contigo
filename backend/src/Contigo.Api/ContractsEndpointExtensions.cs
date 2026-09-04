using Contigo.Documents.Contracts.Application;
using Contigo.SharedKernel;

namespace Contigo.Api;

/// <summary>
/// Maps `PATCH /api/contracts/{id}` (product spec Appendix A API table: "Validated field
/// corrections"; story us-01-correction-history AC-1, task E02/F05/US01/T01). Thin composition
/// per ADR-002 — <see cref="ContractCorrectionService"/> owns the actual versioning/history
/// decisions; this file only translates HTTP &lt;-&gt; the service call, same shape as
/// <see cref="WorkspaceEndpointExtensions"/>/<see cref="AuditEndpointExtensions"/>.
///
/// Same interim `X-Tenant-Id` header placeholder as <c>Program.cs</c>'s document endpoints and
/// <see cref="WorkspaceEndpointExtensions"/> (ADR-010 is not in this task's "Architecture
/// decisions in force" list, so there is no validated caller principal yet) — see
/// <c>Program.cs</c>'s own comment on why this interim gap is not promoted to
/// reports/open-questions.md by this task.
/// </summary>
public static class ContractsEndpointExtensions
{
    public static IEndpointRouteBuilder MapContractsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/api/contracts/{id}", CorrectContractAsync);
        return endpoints;
    }

    private static async Task<IResult> CorrectContractAsync(
        string id,
        ContractCorrectionRequest request,
        HttpRequest httpRequest,
        ContractCorrectionService correctionService,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.Headers.TryGetValue("X-Tenant-Id", out var tenantHeaderValues)
            || !Guid.TryParse(tenantHeaderValues.ToString(), out var tenantGuid))
        {
            return Results.BadRequest("A valid 'X-Tenant-Id' header (a GUID) is required.");
        }

        if (!Guid.TryParse(id, out var contractGuid))
        {
            return Results.BadRequest("The contract id in the route must be a GUID.");
        }

        if (request.Corrections is null || request.Corrections.Count == 0)
        {
            return Results.BadRequest("At least one field correction in 'corrections' is required.");
        }

        var result = await correctionService.CorrectAsync(
            new TenantId(tenantGuid),
            new EntityId(contractGuid),
            request.Corrections,
            request.Reason,
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return string.Equals(result.Error, ContractCorrectionService.ContractNotFoundError, StringComparison.Ordinal)
                ? Results.NotFound()
                : Results.BadRequest(result.Error);
        }

        var correction = result.Value;
        return Results.Ok(new
        {
            contractId = correction.ContractId.Value,
            versionNumber = correction.VersionNumber,
            correctedFields = correction.CorrectedFields,
            correctedAt = correction.CorrectedAt,
        });
    }
}
