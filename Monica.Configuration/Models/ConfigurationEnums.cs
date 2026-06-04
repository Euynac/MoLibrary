using Monica.Configuration.Annotations;

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
/// Defines how Monica derives a Microsoft configuration section path when
/// <see cref="ConfigurationAttribute.SectionPath"/> is not set explicitly.
/// </summary>
public enum ConfigurationSectionPathConvention
{
    /// <summary>
    /// Uses the short CLR type name, for example <c>K8SOptions</c>.
    /// </summary>
    ShortTypeName,

    /// <summary>
    /// Uses the CLR full type name with dots converted to configuration separators.
    /// </summary>
    ClrFullName
}

/// <summary>
/// Defines how Monica reacts when two managed configuration definitions resolve to the same section path.
/// </summary>
public enum ConfigurationDuplicateSectionPathBehavior
{
    /// <summary>
    /// Throws during module startup so ambiguous configuration binding is fixed before the host runs.
    /// </summary>
    FailFast,

    /// <summary>
    /// Logs a warning and allows both definitions to be registered.
    /// </summary>
    Warning
}

/// <summary>
/// Identifies the backing technology of a configuration store.
/// </summary>
public enum ConfigurationStoreKind
{
    File,
    Database
}

/// <summary>
/// Describes the lifecycle state of a stored configuration value.
/// </summary>
public enum ConfigurationValueState
{
    Active,
    Removed
}

/// <summary>
/// Describes the coarse mutation requested by a caller.
/// </summary>
public enum ConfigurationMutationKind
{
    Set,
    Remove
}

/// <summary>
/// Describes whether a mutation targets one scalar leaf or a whole container subtree.
/// </summary>
public enum ConfigurationMutationGranularity
{
    Scalar,
    Container
}

/// <summary>
/// Describes the lifecycle state of a persisted mutation group.
/// </summary>
public enum ConfigurationMutationGroupStatus
{
    /// <summary>
    /// All mutations in the group were applied successfully.
    /// </summary>
    Applied,

    /// <summary>
    /// Only part of the group was applied successfully.
    /// </summary>
    PartiallyApplied,

    /// <summary>
    /// A later rollback group was applied for this group.
    /// </summary>
    RolledBack
}
