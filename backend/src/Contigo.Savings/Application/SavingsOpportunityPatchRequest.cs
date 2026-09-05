namespace Contigo.Savings.Application;

/// <summary>
/// Request body for `PATCH /api/savings/{id}` (task E04/F02/US02/T01, savings-opportunity; parent
/// story us-02-savings-opportunity AC-1 "updates status/owner..."). Deliberately living here rather
/// than in `Contigo.Api` — same
/// `Contigo.ArchitectureTests.DependencyDirectionTests.Host_must_not_contain_domain_types` reason
/// <c>Contigo.Renewals.Application.RenewalActionRequest</c>'s own doc comment already gives.
///
/// Both fields are optional (unlike <c>RenewalActionRequest</c>, which requires all three of its own
/// fields together for its upsert): this is a genuine partial update of an already-identified,
/// already-persisted row keyed by the route's own <c>{id}</c>, not an upsert keyed by a natural key,
/// so a caller supplies only whichever of owner/status it wants to change.
/// <see cref="SavingsOpportunityService.UpdateAsync"/> is the one place that validates/parses either
/// value — <see cref="Status"/> is a plain string, not
/// <see cref="Domain.SavingsOpportunityStatus"/>, for the same reason
/// <c>RenewalActionRequest.Status</c> is.
/// </summary>
public sealed record SavingsOpportunityPatchRequest(string? Owner, string? Status);
