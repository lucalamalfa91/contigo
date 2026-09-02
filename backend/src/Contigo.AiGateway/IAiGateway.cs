namespace Contigo.AiGateway;

/// <summary>
/// AI Gateway interface consumed by domain modules (Documents/Contracts, Chat).
/// Domain modules depend on this abstraction; the implementation behind it
/// is the only place that touches Foundry / Document Intelligence SDKs.
/// </summary>
public interface IAiGateway
{
    // R0 placeholder — concrete operations (OCR, classify, extract, embed, answer)
    // will be added as domain features land.
}
