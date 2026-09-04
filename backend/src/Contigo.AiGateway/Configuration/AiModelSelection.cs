namespace Contigo.AiGateway.Configuration;

/// <summary>
/// One Foundry model binding for an AI Gateway role: which model id (and version) the gateway
/// calls for that role. ADR-004 decision outcome: "each role is bound to a
/// configuration-selected model ID... model swap is config-only" — this record is that binding.
/// </summary>
/// <param name="ModelId">Foundry / Azure AI model or deployment identifier.</param>
/// <param name="ModelVersion">Model or deployment version string.</param>
public sealed record AiModelSelection(string ModelId, string ModelVersion);
