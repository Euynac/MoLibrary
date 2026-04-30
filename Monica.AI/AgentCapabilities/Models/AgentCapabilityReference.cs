namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Identifies one explicit capability reference selected by a chat composer slash command.
/// </summary>
public sealed record AgentCapabilityReference
{
    /// <summary>
    /// Creates a capability reference.
    /// </summary>
    /// <param name="kind">Catalog that owns the referenced capability.</param>
    /// <param name="name">Stable capability name inside its catalog.</param>
    public AgentCapabilityReference(AgentCapabilityKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Kind = kind;
        Name = name.Trim();
    }

    /// <summary>
    /// Catalog that owns the referenced capability.
    /// </summary>
    public AgentCapabilityKind Kind { get; }

    /// <summary>
    /// Stable capability name inside its catalog.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Stable UI key used by chips, slash candidates, and runtime context.
    /// </summary>
    public string Key => CreateKey(Kind, Name);

    /// <summary>
    /// Creates the stable capability key for a kind/name pair.
    /// </summary>
    public static string CreateKey(AgentCapabilityKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return $"{kind}:{name.Trim()}";
    }
}
