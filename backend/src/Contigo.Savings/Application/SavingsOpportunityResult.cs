using Contigo.Savings.Domain;
using Contigo.SharedKernel;

namespace Contigo.Savings.Application;

/// <summary>Outcome of <see cref="SavingsOpportunityService.CreateAsync"/>,
/// <see cref="SavingsOpportunityService.ListAsync"/> or
/// <see cref="SavingsOpportunityService.UpdateAsync"/> — the current, persisted state of one
/// <see cref="SavingsOpportunity"/> (task E04/F02/US02/T01, savings-opportunity).</summary>
/// <param name="RealizedAmount">Task E04/F02/US02/T02 (realized-savings): non-<see langword="null"/>
/// only on the <see cref="SavingsOpportunityService.UpdateAsync"/> call that just recorded it —
/// reflects the <see cref="Domain.RealizedSavings"/> row just written by <em>this</em> call, in the
/// same <see cref="Currency"/> above, never a re-query of this opportunity's full realized-value
/// history (an append-only ledger — see <see cref="Domain.RealizedSavings"/>'s own doc comment);
/// <see langword="null"/> from <see cref="SavingsOpportunityService.CreateAsync"/>/
/// <see cref="SavingsOpportunityService.ListAsync"/> and from any <c>UpdateAsync</c> call that did
/// not itself supply one, even if an earlier call already recorded a realized value for this same
/// opportunity — querying that full history is a follow-up, the same "wiring lands with the first
/// real caller" gap this codebase's other modules already document.</param>
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
    DateTimeOffset UpdatedAt,
    decimal? RealizedAmount = null);
