using System.Text.Json;

namespace Contigo.Quotes.Application.Extraction;

/// <summary>
/// Builds the JSON Schema text <c>Contigo.Api.QuoteExtractionPipeline</c> sends as
/// <c>AiExtractionRequest.JsonSchema</c> for the quote line-item extraction stage (product spec
/// §7.3/§4.4: "schema-constrained output... quantities, SKU/edition, prices, discounts and
/// terms"; ADR-004: "a structured-output-capable model... not free text"). Public (unlike
/// <c>Contigo.Documents.Contracts.Application.Extraction.StagedExtractionJsonSchemas</c>, which is
/// <c>internal</c> to its own module): the AI Gateway call site
/// (<c>Contigo.Api.QuoteExtractionPipeline</c>) lives in a different project from this schema
/// builder — ADR-002 allows <c>Contigo.Api</c> to reference every module, but this type must still
/// be visible across that assembly boundary.
///
/// <b>AC-3 / Appendix C rule 6</b> ("prefer deterministic arithmetic... to LLM reasoning"): this
/// schema deliberately has <b>no</b> computed-total/extended-price property. The model reports only
/// what a person could read directly off the page (quantity, sku, edition, unit price, list price,
/// discount percent, term); <see cref="QuoteLineExtractionService"/> computes
/// <c>QuoteLine.ExtendedPrice</c> (and, when needed, <c>QuoteLine.UnitPrice</c> itself from
/// <c>listPrice</c>/<c>discountPercent</c>) in plain C# arithmetic afterward — the model is
/// structurally incapable of supplying a fabricated total because there is nowhere in this schema
/// for one to go.
///
/// Every item ends in the same evidence tail — <c>sourcePage</c>, <c>sourceSpan</c>,
/// <c>confidence</c> — as every other extraction schema in this codebase, so AC-2 ("every
/// extracted fact carries source span + confidence") holds uniformly. Built via
/// <see cref="JsonSerializer"/> over a plain anonymous C# object rather than hand-written JSON
/// text, so the emitted schema is guaranteed well-formed JSON by construction.
/// </summary>
public static class QuoteLineJsonSchema
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string LineItems()
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                items = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            sku = new { type = new[] { "string", "null" } },
                            edition = new { type = new[] { "string", "null" } },
                            description = new { type = "string" },
                            quantity = new { type = new[] { "number", "null" } },
                            unit = new { type = new[] { "string", "null" } },
                            unitPrice = new { type = new[] { "number", "null" } },
                            listPrice = new { type = new[] { "number", "null" } },
                            discountPercent = new { type = new[] { "number", "null" } },
                            term = new { type = new[] { "string", "null" } },
                            sourcePage = new { type = new[] { "integer", "null" } },
                            sourceSpan = new { type = new[] { "string", "null" } },
                            confidence = new { type = "number", minimum = 0, maximum = 1 },
                        },
                        required = new[] { "description", "confidence" },
                    },
                },
            },
            required = new[] { "items" },
        };

        return JsonSerializer.Serialize(schema, Options);
    }
}
