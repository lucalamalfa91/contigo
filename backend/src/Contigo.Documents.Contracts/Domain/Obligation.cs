using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// A contractual obligation with a due date/recurrence (product spec §6 "Obligation"). Renewal
/// deadline math is computed deterministically downstream (Renewals module, Appendix C rule 6);
/// this row is the extracted, evidenced fact that computation operates on.
/// </summary>
public sealed class Obligation : TenantScopedEntity
{
    public required EntityId ContractId { get; set; }
    public EntityId? SourceDocumentId { get; set; }

    public required string Party { get; set; }
    public required string ObligationType { get; set; }
    public required string Description { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? RecurrenceRule { get; set; }
    public string? Criticality { get; set; }
    public string? Status { get; set; }
    public double? Confidence { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
