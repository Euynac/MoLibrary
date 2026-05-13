namespace Monica.Configuration.Models;

/// <summary>
/// Represents a persisted configuration value payload.
/// </summary>
public sealed record ConfigurationStoredValue
{
    /// <summary>
    /// Gets a value representing JSON null.
    /// </summary>
    public static ConfigurationStoredValue Null { get; } = new()
    {
        Kind = ConfigurationStoredValueKind.PlainJson,
        PlainJson = "null"
    };

    /// <summary>
    /// Gets the storage payload kind.
    /// </summary>
    public ConfigurationStoredValueKind Kind { get; init; }

    /// <summary>
    /// Gets the plain JSON payload for non-sensitive values.
    /// </summary>
    public string? PlainJson { get; init; }

    /// <summary>
    /// Gets the protected payload for sensitive values.
    /// </summary>
    public string? ProtectedPayload { get; init; }

    /// <summary>
    /// Gets the external secret reference when the value is stored out of band.
    /// </summary>
    public string? SecretReference { get; init; }

    /// <summary>
    /// Creates a plain JSON stored value.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The stored value.</returns>
    public static ConfigurationStoredValue Plain(string json)
    {
        return new ConfigurationStoredValue
        {
            Kind = ConfigurationStoredValueKind.PlainJson,
            PlainJson = json
        };
    }
}
