using Monica.Core.Modularity.Interfaces;

namespace Monica.StateStore.Providers;

/// <summary>
/// StateStore Provider type enumeration
/// </summary>
public enum EStateStoreProviderType
{
    /// <summary>
    /// Memory cache provider
    /// </summary>
    Memory,

    /// <summary>
    /// Redis provider
    /// </summary>
    Redis,

    /// <summary>
    /// Dapr StateStore provider
    /// </summary>
    Dapr,

    /// <summary>
    /// Unknown provider type
    /// </summary>
    Unknown
}

/// <summary>
/// StateStore provider capability flags
/// </summary>
[Flags]
public enum EStateStoreCapabilities
{
    /// <summary>
    /// No special capabilities
    /// </summary>
    None = 0,

    /// <summary>
    /// Supports key scanning (ScanKeysAsync)
    /// </summary>
    KeyScanning = 1 << 0,

    /// <summary>
    /// Supports retrieving the raw serialized text of stored state values
    /// </summary>
    RawStringRetrieval = 1 << 1,

    /// <summary>
    /// Supports query state (QueryStateAsync)
    /// </summary>
    QueryState = 1 << 2,

    /// <summary>
    /// Supports bulk operations
    /// </summary>
    BulkOperations = 1 << 3
}

/// <summary>
/// Extended provider interface for StateStore modules.
/// Provides metadata about the provider's capabilities.
/// </summary>
public interface IStateStoreModuleProvider : IMoModuleProvider
{
    /// <summary>
    /// Gets the type of this StateStore provider
    /// </summary>
    EStateStoreProviderType ProviderType { get; }

    /// <summary>
    /// Gets the capabilities supported by this provider
    /// </summary>
    EStateStoreCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the display name for this provider
    /// </summary>
    string DisplayName { get; }
}
