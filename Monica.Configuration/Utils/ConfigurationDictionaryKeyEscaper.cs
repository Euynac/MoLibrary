namespace Monica.Configuration.Utils;

/// <summary>
/// Validates dictionary keys that must be projected into Microsoft configuration paths.
/// </summary>
public static class ConfigurationDictionaryKeyEscaper
{
    /// <summary>
    /// Validates whether a dynamic key can be represented as one Microsoft configuration path segment.
    /// </summary>
    /// <param name="key">The key to validate.</param>
    /// <param name="problem">The developer-facing validation problem when the key is invalid.</param>
    /// <returns>True when the key can be projected safely; otherwise, false.</returns>
    public static bool TryValidateForProjection(string? key, out string? problem)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            problem = "Keys used in editable configuration paths cannot be empty or whitespace.";
            return false;
        }

        if (key.Contains(':', StringComparison.Ordinal))
        {
            problem = "Keys used in editable configuration paths cannot contain ':'.";
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>
    /// Throws when a dictionary key cannot be safely projected.
    /// </summary>
    /// <param name="key">The dictionary key.</param>
    public static void ThrowIfInvalidForProjection(string key)
    {
        if (!TryValidateForProjection(key, out var problem))
        {
            throw new ArgumentException(problem, nameof(key));
        }
    }
}
