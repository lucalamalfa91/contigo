using Contigo.SharedKernel;

namespace Contigo.Quotes.Domain;

/// <summary>
/// A single priced line on a <see cref="Quote"/> (task E05/F01/US01/T01, quote-extraction; parent
/// story us-01-quote-line-extraction AC-2 "Line items extract quantity/SKU/edition/price/
/// discount/term (evidence + confidence)"; product spec §4.4 "Extract line items, quantities,
/// SKU/edition, prices, discounts and terms"; spec §6 "Quote / QuoteLine | ... line-level
/// product/SKU, quantity, unit price, discount, cost"). Mirrors
/// <c>Contigo.Documents.Contracts.Domain.ContractLineItem</c>'s own "one row = one fact" shape —
/// every field the AI Gateway `extract` role proposes carries the same source span/page/confidence
/// tail (Appendix C rule 2: "never show a consequential extracted fact without source evidence and
/// confidence metadata") directly on this row, not via a separate evidence table (that side-table
/// shape only exists for <c>Contract</c>'s own scalar-field stages, which aggregate many
/// independently-extracted facts onto one row — see <c>Contigo.Documents.Contracts.Domain
/// .ExtractionEvidence</c>'s own doc comment; a <see cref="QuoteLine"/> row, like a
/// <c>ContractLineItem</c> row, already is one fact).
/// </summary>
public sealed class QuoteLine : TenantScopedEntity
{
    public required EntityId QuoteId { get; set; }

    public string? Sku { get; set; }

    /// <summary>Product/licensing edition (e.g. "Enterprise", "Standard") — spec §4.4's own
    /// "SKU/edition" pairing; a field <c>ContractLineItem</c> has no equivalent for, since no
    /// contract-extraction task has needed it (a new column there, if ever needed, is that task's
    /// own migration to add, not backfilled here).</summary>
    public string? Edition { get; set; }

    public required string Description { get; set; }

    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }

    /// <summary>The quoted per-unit price. When the extraction payload reports
    /// <see cref="ListPrice"/> and <see cref="DiscountPercent"/> but not this field directly,
    /// <c>Contigo.Quotes.Application.Extraction.QuoteLineExtractionService</c> derives it
    /// deterministically in code (never asks the model to do the arithmetic) — see that type's own
    /// doc comment (AC-3 / Appendix C rule 6: "prefer deterministic arithmetic... to LLM
    /// reasoning").</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Undiscounted per-unit list price, when the source document states one — the basis
    /// <see cref="DiscountPercent"/> is applied against.</summary>
    public decimal? ListPrice { get; set; }

    /// <summary>Percentage off <see cref="ListPrice"/> (0-100), when the source document expresses
    /// the discount as a rate — spec §4.4's "discounts". Mirrors
    /// <c>ContractLineItem.Discount</c>'s own shape/range.</summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>Free-text billing/commitment term (e.g. "Annual", "36 months", "One-time") — spec
    /// §4.4's "terms". Not modelled as an enum: no ADR or spec fixes a closed vocabulary, the same
    /// "free text, not a closed enum" treatment <c>ContractLineItem.BillingPeriod</c> already
    /// gets.</summary>
    public string? Term { get; set; }

    /// <summary>Deterministic <see cref="Quantity"/> × <see cref="UnitPrice"/>, computed in code by
    /// <c>QuoteLineExtractionService</c> — never a value the AI Gateway `extract` role is asked to
    /// report (AC-3 "Separate arithmetic from LLM language"; Appendix C rule 6). <see
    /// langword="null"/> when either factor is missing, never a guessed default.</summary>
    public decimal? ExtendedPrice { get; set; }

    /// <summary>Evidence pointer + page/section span + confidence (Appendix C rule 2; spec §7.3
    /// "every extracted fact carries source span + confidence"), mirroring
    /// <c>ContractLineItem.SourceSpan</c>/<c>SourcePage</c>/<c>Confidence</c>.</summary>
    public string? SourceSpan { get; set; }
    public int? SourcePage { get; set; }
    public double? Confidence { get; set; }

    /// <summary>
    /// Canonical, case/whitespace-normalized form of <see cref="Sku"/> (task E05/F01/US02/T01,
    /// sku-normalization; parent story us-02-sku-normalization AC-1 "Normalize SKU/edition to the
    /// canonical product mapping"), computed by
    /// <c>Contigo.Quotes.Application.Normalization.SkuNormalizationService</c> — never set
    /// directly by extraction. <see langword="null"/> exactly when <see cref="Sku"/> itself is
    /// null/blank; see <see cref="MatchStatus"/>'s own doc comment for what that implies.
    /// </summary>
    public string? NormalizedSku { get; set; }

    /// <summary>Canonical, case/whitespace-normalized form of <see cref="Edition"/> — same
    /// treatment as <see cref="NormalizedSku"/>, computed alongside it.</summary>
    public string? NormalizedEdition { get; set; }

    /// <summary>
    /// Whether <see cref="NormalizedSku"/> resolves to a known
    /// <see cref="SkuProductMapping"/> for this tenant (task E05/F01/US02/T01, sku-normalization
    /// AC-1/AC-2). Defaults to <see cref="SkuMatchStatus.Unmatched"/> — the conservative "needs
    /// attention until normalization actually runs and says otherwise" state, never a silent
    /// "assume fine" default (Appendix C rule 10) — until
    /// <c>SkuNormalizationService.NormalizeAsync</c> sets the real value immediately after
    /// extraction (<c>Contigo.Api.QuoteExtractionPipeline</c>) and again on every future
    /// recalculate (task E05/F01/US02/T02). See <see cref="SkuMatchStatus"/>'s own doc comment for
    /// what each value means and the spec §11.3 guardrail this status exists to serve.
    /// </summary>
    public SkuMatchStatus MatchStatus { get; set; } = SkuMatchStatus.Unmatched;

    public required DateTimeOffset CreatedAt { get; set; }
}
