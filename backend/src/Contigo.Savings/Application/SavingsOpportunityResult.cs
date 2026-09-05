using Contigo.Savings.Domain;
using Contigo.SharedKernel;

namespace Contigo.Savings.Application;

/// <summary>Outcome of <see cref="SavingsOpportunityService.CreateAsync"/>,
/// <see cref="SavingsOpportunityService.ListAsync"/> or
/// <see cref="SavingsOpportunityService.UpdateAsync"/> — the current, persisted state of one
/// <see cref="SavingsOpportunity"/> (task E04/F02/US02/T01, savings-opportunity).</summary>
public sealed record SavingsOpportunityResult(
    EntityId Id,
    EntityId? SupplierId,
    EntityId? ContractId,
    string Type,
    decimal CurrentSpend,
    string Currency,
    decimal EstimatedSavingsLow,
    decimal EstimatedSavingsHigh,
    double Confidence,
    SavingsOpportunityStatus Status,
    string? Owner,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
