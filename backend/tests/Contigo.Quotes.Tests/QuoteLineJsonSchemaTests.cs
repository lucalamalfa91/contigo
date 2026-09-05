using System.Text.Json;
using Contigo.Quotes.Application.Extraction;

namespace Contigo.Quotes.Tests;

/// <summary>
/// Proves <see cref="QuoteLineJsonSchema.LineItems"/> is well-formed and shapes AC-2/AC-3 (task
/// E05/F01/US01/T01, quote-extraction) structurally, not just by doc-comment intent — mirrors the
/// spirit of <c>Contigo.Documents.Contracts.Tests.ContractLineItemSchemaTests</c>/
/// <c>ContractEvidenceSchemaTests</c> (schema-shape proofs for the sibling contract pipeline).
/// </summary>
public sealed class QuoteLineJsonSchemaTests
{
    private static readonly string[] ExpectedItemFields =
    [
        "sku", "edition", "description", "quantity", "unit", "unitPrice", "listPrice",
        "discountPercent", "term", "sourcePage", "sourceSpan", "confidence",
    ];

    /// <summary>AC-3 ("separate arithmetic from LLM language"): no computed money field exists
    /// anywhere in the schema for a model to (mis)report — <see cref="QuoteLineExtractionService"/>
    /// is structurally the only place a total can come from.</summary>
    private static readonly string[] ForbiddenComputedFields =
    ["extendedPrice", "totalPrice", "totalCost", "annualCost", "netPrice", "total"];

    [Fact]
    public void Produces_well_formed_json()
    {
        var schema = QuoteLineJsonSchema.LineItems();

        using var document = JsonDocument.Parse(schema);

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public void Item_schema_requests_exactly_the_ac2_fields_plus_the_evidence_tail()
    {
        var itemProperties = GetItemProperties();

        foreach (var expected in ExpectedItemFields)
        {
            Assert.True(
                itemProperties.TryGetProperty(expected, out _),
                $"Expected the quote line-item schema to declare a '{expected}' property.");
        }
    }

    [Fact]
    public void Description_and_confidence_are_required()
    {
        var itemSchema = GetItemSchema();
        var required = itemSchema.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.Contains("description", required);
        Assert.Contains("confidence", required);
    }

    [Fact]
    public void Never_asks_the_model_to_report_a_computed_total_ac3()
    {
        var itemProperties = GetItemProperties();

        foreach (var forbidden in ForbiddenComputedFields)
        {
            Assert.False(
                itemProperties.TryGetProperty(forbidden, out _),
                $"AC-3 violation: the quote line-item schema must never ask the model for a " +
                $"computed '{forbidden}' — that arithmetic belongs in " +
                $"{nameof(QuoteLineExtractionService)}, not the LLM prompt.");
        }
    }

    [Fact]
    public void Confidence_is_bounded_zero_to_one()
    {
        var itemProperties = GetItemProperties();
        var confidence = itemProperties.GetProperty("confidence");

        Assert.Equal(0, confidence.GetProperty("minimum").GetInt32());
        Assert.Equal(1, confidence.GetProperty("maximum").GetInt32());
    }

    private static JsonElement GetItemSchema()
    {
        using var document = JsonDocument.Parse(QuoteLineJsonSchema.LineItems());
        return document.RootElement.Clone()
            .GetProperty("properties")
            .GetProperty("items")
            .GetProperty("items");
    }

    private static JsonElement GetItemProperties() => GetItemSchema().GetProperty("properties");
}
