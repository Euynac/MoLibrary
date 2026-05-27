namespace Monica.Configuration.Models;

/// <summary>
/// Represents a JSON node value recorded by mutation and history operations.
/// </summary>
public sealed record ConfigurationStoredValue
{
    /// <summary>
    /// Gets a value representing JSON null.
    /// </summary>
    public static ConfigurationStoredValue Null { get; } = new()
    {
        Json = "null"
    };

    /// <summary>
    /// Gets the JSON payload.
    /// </summary>
    public required string Json { get; init; }

    /// <summary>
    /// Creates a stored JSON node value.
    /// </summary>
    /// <param name="json">The JSON payload.</param>
    /// <returns>The stored value.</returns>
    public static ConfigurationStoredValue FromJson(string json)
    {
        return new ConfigurationStoredValue
        {
            Json = json
        };
    }
}
