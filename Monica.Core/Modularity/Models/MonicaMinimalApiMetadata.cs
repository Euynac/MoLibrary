namespace Monica.Core.Modularity.Models;

/// <summary>
/// Marks Minimal API endpoints that are owned by Monica modules so downstream tooling can classify them without assembly inference.
/// </summary>
public sealed class MonicaMinimalApiMetadata
{
    /// <summary>
    /// Shared marker instance used for Monica-owned Minimal API endpoints.
    /// </summary>
    public static MonicaMinimalApiMetadata Instance { get; } = new();

    private MonicaMinimalApiMetadata()
    {
    }
}
