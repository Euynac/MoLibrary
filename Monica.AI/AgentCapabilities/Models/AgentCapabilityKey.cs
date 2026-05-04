namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Creates stable keys for Monica agent capabilities across management and chat UI surfaces.
/// </summary>
public static class AgentCapabilityKey
{
    /// <summary>
    /// Creates a stable key for a capability kind/name pair.
    /// </summary>
    /// <param name="kind">Catalog that owns the capability.</param>
    /// <param name="name">Stable capability name inside the catalog.</param>
    /// <returns>A stable case-preserving key suitable for UI identity and comparisons.</returns>
    public static string Create(AgentCapabilityKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return $"{kind}:{name.Trim()}";
    }
}
