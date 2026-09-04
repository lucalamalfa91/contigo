namespace Contigo.Renewals.Domain;

/// <summary>
/// Contract-risk input for
/// <see cref="Contigo.Renewals.Application.PriorityScoreCalculator"/>'s contract-risk component
/// (product spec §9.2 "Priority Score = ... + Contract Risk"; task E03/F01/US02/T01, parent story
/// us-02-priority-score AC-1).
///
/// <para>
/// Mirrors <c>Contigo.Documents.Contracts.Domain.RiskSeverity</c>'s four levels rather than
/// referencing that type: ADR-002's dependency-direction rule allows <c>Contigo.Renewals</c> to
/// reference only <c>Contigo.SharedKernel</c> and <c>Contigo.Benchmark</c>
/// (<c>Contigo.ArchitectureTests.DependencyDirectionTests</c>'s allow-list for this module; see
/// <c>backend/README.md</c>'s "Dependency direction" table) — the same shape decision
/// <see cref="Contigo.Renewals.Application.ContractRenewalTerms"/> and
/// <c>Contigo.Chat.Application.ContractFact</c> already made for the same reason. A composition
/// root maps <c>PortfolioListItem.Risk</c> (the highest <c>RiskSeverity</c> across a contract's
/// <c>Risk</c> rows) onto this enum 1:1; no task in this wave wires that composition yet — the
/// same "caller supplies it however it likes today, a real mapping lands later" gap
/// <see cref="Contigo.Renewals.Application.ContractRenewalTerms"/>'s own doc comment already
/// documents for this module.
/// </para>
/// </summary>
public enum ContractRiskLevel
{
    Low,
    Medium,
    High,
    Critical,
}
