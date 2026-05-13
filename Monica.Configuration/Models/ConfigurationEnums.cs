namespace Monica.Configuration.Models;

/// <summary>
/// Describes the structural role of a configuration schema node.
/// </summary>
public enum ConfigurationNodeKind
{
    Object,
    Dictionary,
    List,
    Scalar
}

/// <summary>
/// Describes the scalar value category exposed by a configuration node.
/// </summary>
public enum ConfigurationValueKind
{
    String,
    Boolean,
    Integer,
    Decimal,
    Floating,
    Enum,
    DateTime,
    TimeSpan,
    Uri,
    Json
}

/// <summary>
/// Describes when a configuration value can be applied by a running process.
/// </summary>
public enum ConfigurationReloadBehavior
{
    /// <summary>
    /// Inherits the reload behavior from the owning definition or parent node.
    /// </summary>
    Inherit,

    /// <summary>
    /// The value can be applied by reloading configuration in the running process.
    /// </summary>
    OnlineReloadable,

    /// <summary>
    /// The value is read dynamically but requires a process restart before it is observed safely.
    /// </summary>
    RequiresRestart,

    /// <summary>
    /// The value is intentionally fixed after startup and should not be treated as hot-reloadable.
    /// </summary>
    StaticAfterStartup
}

/// <summary>
/// Identifies the backing technology of a configuration value source.
/// </summary>
public enum ConfigurationSourceKind
{
    JsonFile,
    Database,
    Redis,
    DaprConfiguration,
    Environment,
    Memory,
    SecretStore
}

/// <summary>
/// Describes how an override participates in the merge process.
/// </summary>
public enum ConfigurationValueState
{
    Active,
    RemovedOverride,
    RemovedSubtree
}

/// <summary>
/// Describes the coarse mutation requested by a caller.
/// </summary>
public enum ConfigurationMutationKind
{
    Set,
    Remove,
    Replace
}

/// <summary>
/// Describes how a stored value payload is persisted.
/// </summary>
public enum ConfigurationStoredValueKind
{
    PlainJson,
    ProtectedJson,
    SecretReference
}

/// <summary>
/// Describes whether an override targets one scalar leaf or a whole container subtree.
/// </summary>
public enum ConfigurationOverrideGranularity
{
    Scalar,
    Container
}
