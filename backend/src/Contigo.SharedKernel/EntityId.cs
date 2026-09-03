namespace Contigo.SharedKernel;

/// <summary>
/// Strongly-typed identifier for domain entities.
/// Wraps a GUID to prevent accidental mixing of entity IDs across bounded contexts.
/// </summary>
public readonly record struct EntityId(Guid Value)
{
    public static EntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
