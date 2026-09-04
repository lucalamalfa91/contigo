using Contigo.SharedKernel;

namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// A single priced line on a <see cref="Contract"/> — one row of the commercial/pricing table
/// (product spec §6 "ContractLineItem"): the product/SKU purchased, quantity, unit economics,
/// and billing cadence. <see cref="ProductId"/> is a cross-module reference by id only
/// (Suppliers/Products owns the Product aggregate, per the module map's dependency-direction
/// rule) — no physical FK crosses that bounded-context boundary, the same treatment
/// <see cref="Contract.SupplierId"/> already gets.
/// </summary>
public sealed class ContractLineItem : TenantScopedEntity
{
    public required EntityId ContractId { get; set; }
    public EntityId? ProductId { get; set; }

    public string? Sku { get; set; }
    public required string Description { get; set; }

    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? ListPrice { get; set; }

    /// <summary>Percentage off list price (0-100), when the source document expresses the
    /// discount as a rate rather than only as a lower <see cref="UnitPrice"/>.</summary>
    public decimal? Discount { get; set; }

    public string? BillingPeriod { get; set; }
    public decimal? AnnualCost { get; set; }
    public decimal? TotalCost { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
