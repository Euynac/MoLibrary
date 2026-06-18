namespace Monica.Configuration.Models;

/// <summary>
/// Describes why a configuration definition publish history entry was recorded.
/// </summary>
public enum ConfigurationDefinitionPublishChangeKind
{
    /// <summary>
    /// The definition was published for the first time.
    /// </summary>
    Created,

    /// <summary>
    /// The definition schema changed.
    /// </summary>
    SchemaChanged,

    /// <summary>
    /// Definition metadata changed without changing the schema hash.
    /// </summary>
    MetadataChanged
}
