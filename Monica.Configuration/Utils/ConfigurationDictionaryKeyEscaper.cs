namespace Monica.Configuration.Utils;

/// <summary>
/// Validates dictionary keys that must be projected into Microsoft configuration paths.
/// </summary>
public static class ConfigurationDictionaryKeyEscaper
{
    /// <summary>
    /// Throws when a dictionary key cannot be safely projected.
    /// </summary>
    /// <param name="key">The dictionary key.</param>
    public static void ThrowIfInvalidForProjection(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("Dictionary keys used in editable configuration paths cannot contain ':'.", nameof(key));
        }
    }
}
