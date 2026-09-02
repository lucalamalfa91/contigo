namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// A bounded, schema-constrained extraction task (product spec §7.2: "Avoid one giant prompt.
/// Split extraction into bounded, schema-constrained tasks"). Each stage is its own
/// <see cref="ExtractionJob"/>.
/// </summary>
public enum ExtractionStage
{
    Classification,
    Metadata,
    CommercialTerms,
    DatesAndRenewalTerms,
    LineItems,
    LegalClauses,
}
