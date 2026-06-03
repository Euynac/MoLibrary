namespace Monica.Configuration.Models;

/// <summary>
/// Describes all runtime sources that contribute a value for one configuration path.
/// </summary>
public sealed record ConfigurationSourceChain
{
    /// <summary>
    /// Gets the owning configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the logical path inside the configuration definition.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path.
    /// </summary>
    public required string ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the source values ordered from highest priority to lowest priority.
    /// </summary>
    public IReadOnlyList<ConfigurationSourceValue> Values { get; init; } = [];
}

/// <summary>
/// Describes one source's contribution to a configuration path.
/// </summary>
public sealed record ConfigurationSourceValue
{
    /// <summary>
    /// Gets the source descriptor.
    /// </summary>
    public required ConfigurationSourceDescriptor Source { get; init; }

    /// <summary>
    /// Gets the source value. Sensitive values are null.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets whether this source currently wins the Microsoft configuration stack.
    /// </summary>
    public bool IsEffective { get; init; }

    /// <summary>
    /// Gets whether the display value was redacted.
    /// </summary>
    public bool IsSensitive { get; init; }
}

/// <summary>
/// Describes source contribution counts for a whole configuration definition.
/// </summary>
public sealed record ConfigurationDefinitionSourceContribution
{
    /// <summary>
    /// Gets the source descriptor.
    /// </summary>
    public required ConfigurationSourceDescriptor Source { get; init; }

    /// <summary>
    /// Gets the number of scalar values supplied by this source for the definition.
    /// </summary>
    public int SuppliedValueCount { get; init; }

    /// <summary>
    /// Gets the number of scalar values where this source is the effective winner.
    /// </summary>
    public int EffectiveValueCount { get; init; }
}

/// <summary>
/// Describes all managed configuration values supplied by one runtime source.
/// </summary>
public sealed record ConfigurationSourceInventory
{
    /// <summary>
    /// Gets the inspected runtime source.
    /// </summary>
    public required ConfigurationSourceDescriptor Source { get; init; }

    /// <summary>
    /// Gets the number of managed scalar values supplied by this source.
    /// </summary>
    public int SuppliedValueCount { get; init; }

    /// <summary>
    /// Gets the number of supplied scalar values where this source currently wins.
    /// </summary>
    public int EffectiveValueCount { get; init; }

    /// <summary>
    /// Gets the supplied managed scalar values.
    /// </summary>
    public IReadOnlyList<ConfigurationSourceInventoryItem> Items { get; init; } = [];
}

/// <summary>
/// Describes one managed configuration value supplied by a runtime source.
/// </summary>
public sealed record ConfigurationSourceInventoryItem
{
    /// <summary>
    /// Gets the owning definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the owning definition display name.
    /// </summary>
    public required string DefinitionDisplayName { get; init; }

    /// <summary>
    /// Gets the projected Microsoft configuration path.
    /// </summary>
    public required string ConfigurationPath { get; init; }

    /// <summary>
    /// Gets the path relative to the owning definition section.
    /// </summary>
    public required string RelativeConfigurationPath { get; init; }

    /// <summary>
    /// Gets the schema node display name when the path can be resolved.
    /// </summary>
    public string? NodeLabel { get; init; }

    /// <summary>
    /// Gets the display-safe source value. Sensitive values are null.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets whether the source value was redacted.
    /// </summary>
    public bool IsSensitive { get; init; }

    /// <summary>
    /// Gets whether this source currently wins for the path.
    /// </summary>
    public bool IsEffective { get; init; }
}

/// <summary>
/// Describes a display-safe configuration file payload.
/// </summary>
public sealed record ConfigurationSourceFileView
{
    /// <summary>
    /// Gets the source descriptor.
    /// </summary>
    public required ConfigurationSourceDescriptor Source { get; init; }

    /// <summary>
    /// Gets the normalized JSON content. Known sensitive paths are redacted.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets whether sensitive managed values were redacted.
    /// </summary>
    public bool HasRedactions { get; init; }
}

/// <summary>
/// Describes a requested mutation against a specific external configuration source.
/// </summary>
public sealed record ConfigurationSourceMutationRequest
{
    /// <summary>
    /// Gets the target source key.
    /// </summary>
    public required string SourceKey { get; init; }

    /// <summary>
    /// Gets the target configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the target logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the mutation kind.
    /// </summary>
    public ConfigurationMutationKind MutationKind { get; init; }

    /// <summary>
    /// Gets the JSON value payload.
    /// </summary>
    public required ConfigurationStoredValue Value { get; init; }

    /// <summary>
    /// Gets the expected schema version.
    /// </summary>
    public int ExpectedSchemaVersion { get; init; }

    /// <summary>
    /// Gets the expected source content revision hash.
    /// </summary>
    public string? ExpectedSourceRevision { get; init; }

    /// <summary>
    /// Gets the mutation context.
    /// </summary>
    public ConfigurationMutationContext Context { get; init; } = new();
}
