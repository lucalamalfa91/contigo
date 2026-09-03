namespace Contigo.Documents.Contracts.Domain;

/// <summary>
/// Contract hierarchy document kinds (product spec §6.1: Supplier └── Contract Family ├── MSA
/// ├── Order Form ├── Amendment ├── SOW └── Renewal Letter). Shared by <see cref="Contract"/>
/// (what kind of contract this is) and <see cref="Document"/> (what kind of file was uploaded) —
/// in practice one uploaded file usually *is* one of these kinds.
/// </summary>
public enum ContractDocumentType
{
    Msa,
    OrderForm,
    Amendment,
    Sow,
    RenewalLetter,
    Other,
}
