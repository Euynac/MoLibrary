namespace Monica.Configuration.Models;

/// <summary>
/// Describes one runtime provider participating in the Microsoft configuration stack.
/// </summary>
public sealed record ConfigurationSourceDescriptor
{
    /// <summary>
    /// Gets the stable runtime source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the provider index in the Microsoft configuration root. Higher indexes have higher priority.
    /// </summary>
    public int PriorityIndex { get; init; }

    /// <summary>
    /// Gets the operator-facing source name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the concrete provider type name.
    /// </summary>
    public required string ProviderType { get; init; }

    /// <summary>
    /// Gets the source kind.
    /// </summary>
    public ConfigurationSourceKind Kind { get; init; }

    /// <summary>
    /// Gets whether this source was registered through Monica.Configuration registration APIs.
    /// </summary>
    public bool IsManagedByMonica { get; init; }

    /// <summary>
    /// Gets whether this source can be edited by Monica.Configuration.
    /// </summary>
    public bool IsWritable { get; init; }

    /// <summary>
    /// Gets why this source is not writable, when applicable.
    /// </summary>
    public string? ReadOnlyReason { get; init; }

    /// <summary>
    /// Gets the configuration file path, when this source is file-backed.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Gets the resolved physical file path, when known.
    /// </summary>
    public string? PhysicalPath { get; init; }

    /// <summary>
    /// Gets whether the file source is optional.
    /// </summary>
    public bool? Optional { get; init; }

    /// <summary>
    /// Gets whether the file source reloads when the file changes.
    /// </summary>
    public bool? ReloadOnChange { get; init; }

    /// <summary>
    /// Gets an optional developer-provided description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Describes the broad provider technology kind.
/// </summary>
public enum ConfigurationSourceKind
{
    /// <summary>
    /// Monica's effective-value provider.
    /// </summary>
    MonicaEffectiveStore,

    /// <summary>
    /// JSON file provider.
    /// </summary>
    JsonFile,

    /// <summary>
    /// Environment variable provider.
    /// </summary>
    EnvironmentVariables,

    /// <summary>
    /// Command-line provider.
    /// </summary>
    CommandLine,

    /// <summary>
    /// In-memory provider.
    /// </summary>
    Memory,

    /// <summary>
    /// Provider type that Monica can inspect only generically.
    /// </summary>
    Unsupported
}

/// <summary>
/// Describes the target storage that a configuration mutation writes.
/// </summary>
public enum ConfigurationMutationTargetKind
{
    /// <summary>
    /// The mutation writes Monica's active effective-value store.
    /// </summary>
    MonicaEffectiveStore,

    /// <summary>
    /// The mutation writes one external Microsoft configuration source.
    /// </summary>
    ExternalConfigurationSource
}
