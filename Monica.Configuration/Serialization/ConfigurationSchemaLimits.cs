namespace Monica.Configuration.Serialization;

/// <summary>
/// Defines the bounded traversal contract shared by CLR scanning and persisted schema serialization.
/// </summary>
internal static class ConfigurationSchemaLimits
{
    /// <summary>
    /// Gets the deepest logical schema path accepted below the root, whose depth is zero.
    /// </summary>
    internal const int MAX_LOGICAL_DEPTH = 64;

    /// <summary>
    /// Gets the maximum JSON reader and writer depth for the compact persisted schema representation.
    /// </summary>
    internal const int MAX_PERSISTED_JSON_DEPTH = 256;
}
