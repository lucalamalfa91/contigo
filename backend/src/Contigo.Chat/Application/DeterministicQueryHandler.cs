using Contigo.Chat.Domain;
using Contigo.SharedKernel;

namespace Contigo.Chat.Application;

/// <summary>
/// Executes a <see cref="DeterministicQuery"/> (produced by <see cref="DeterministicQueryPlanner"/>)
/// against an already-fetched, already-tenant-scoped <see cref="ContractFact"/> snapshot — the
/// "hit deterministic queries/filters (no LLM)" step parent story us-01-query-router AC-2
/// requires. Pure and synchronous: no database call, no HTTP call, no LLM call, so the same
/// inputs always produce the same <see cref="DeterministicQueryResult"/> (Appendix C rule 6).
///
/// <para>
/// Takes <see cref="IClock"/> (not <see cref="DateTimeOffset.UtcNow"/> directly) for the "today" a
/// <see cref="DeterministicQueryKind.RenewalWindow"/> query is measured from — the same
/// determinism convention every other date-sensitive service in this solution already follows
/// (for example <c>Contigo.Documents.Contracts.Application.DocumentUploadService</c>), so a test
/// can fix "now" instead of racing the wall clock.
/// </para>
/// </summary>
public sealed class DeterministicQueryHandler(IClock clock)
{
    public DeterministicQueryResult Handle(DeterministicQuery query, IReadOnlyList<ContractFact> contracts)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(contracts);

        return query switch
        {
            DeterministicQuery.RenewalWindow renewalWindow => HandleRenewalWindow(renewalWindow, contracts),
            DeterministicQuery.AnnualSpend annualSpend => HandleAnnualSpend(annualSpend, contracts),
            DeterministicQuery.Unsupported unsupported => HandleUnsupported(unsupported),
            _ => throw new NotSupportedException(
                $"No deterministic handler exists for query type '{query.GetType().Name}'."),
        };
    }

    private DeterministicQueryResult HandleRenewalWindow(
        DeterministicQuery.RenewalWindow query, IReadOnlyList<ContractFact> contracts)
    {
        var asOf = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var horizon = asOf.AddDays(query.Days);

        // A contract only "renews" (as opposed to merely "ends") when AutoRenewal is true — the
        // same rule PortfolioListItem.RenewalDate already documents; EndDate is the effective
        // renewal date. A contract whose EndDate already passed is not "renewing in the next N
        // days" (Appendix C rule 10 — never report a lapsed contract as an upcoming renewal).
        var matches = contracts
            .Where(c => c.AutoRenewal && c.EndDate is { } endDate && endDate >= asOf && endDate <= horizon)
            .Select(c => c.ContractId)
            .ToList();

        return new DeterministicQueryResult(
            query.Question,
            DeterministicQueryKind.RenewalWindow,
            matches,
            AggregateAnnualSpend: null,
            $"{matches.Count} contract(s) auto-renew between {asOf:yyyy-MM-dd} and {horizon:yyyy-MM-dd} " +
            "(deterministic filter on Contract.AutoRenewal/EndDate, Appendix C rule 6 — no LLM).");
    }

    private static DeterministicQueryResult HandleAnnualSpend(
        DeterministicQuery.AnnualSpend query, IReadOnlyList<ContractFact> contracts)
    {
        // Null AnnualSpend is excluded, not treated as zero (ContractFact's own doc comment): an
        // unvalidated/missing figure must never silently understate the total.
        var matches = contracts
            .Where(c => query.SupplierId is null || c.SupplierId == query.SupplierId)
            .Where(c => c.AnnualSpend is not null)
            .ToList();

        var total = matches.Sum(c => c.AnnualSpend!.Value);

        var scope = query.SupplierId is { } supplierId
            ? $"supplier {supplierId}"
            : "all suppliers (no supplier filter resolved yet — see DeterministicQueryPlanner)";

        return new DeterministicQueryResult(
            query.Question,
            DeterministicQueryKind.AnnualSpend,
            matches.Select(c => c.ContractId).ToList(),
            total,
            $"Summed Contract.AnnualSpend across {matches.Count} contract(s) for {scope} " +
            "(deterministic aggregation, Appendix C rule 6 — no LLM; assumes a single reporting " +
            "currency — no cross-contract currency normalization exists anywhere in this " +
            "codebase yet).");
    }

    private static DeterministicQueryResult HandleUnsupported(DeterministicQuery.Unsupported query) =>
        new(query.Question, DeterministicQueryKind.Unsupported, [], null, query.Reason);
}
