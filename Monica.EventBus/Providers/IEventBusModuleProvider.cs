using Monica.Core.Module.Interfaces;

namespace Monica.EventBus.Providers;

/// <summary>
/// EventBus Provider type enumeration
/// </summary>
public enum EEventBusProviderType
{
    /// <summary>
    /// Local in-memory event bus
    /// </summary>
    Local,

    /// <summary>
    /// Dapr pub/sub event bus
    /// </summary>
    Dapr,

    /// <summary>
    /// Unknown provider type
    /// </summary>
    Unknown
}

/// <summary>
/// EventBus provider capability flags
/// </summary>
[Flags]
public enum EEventBusCapabilities
{
    /// <summary>
    /// No special capabilities
    /// </summary>
    None = 0,

    /// <summary>
    /// Supports bulk publish operations
    /// </summary>
    BulkPublish = 1 << 0,

    /// <summary>
    /// Supports streaming subscriptions
    /// </summary>
    Streaming = 1 << 1,

    /// <summary>
    /// Supports dead letter queue
    /// </summary>
    DeadLetterQueue = 1 << 2
}

/// <summary>
/// Extended provider interface for EventBus modules.
/// Provides metadata about the provider's capabilities.
/// </summary>
public interface IEventBusModuleProvider : IMoModuleProvider
{
    /// <summary>
    /// Gets the type of this EventBus provider
    /// </summary>
    EEventBusProviderType ProviderType { get; }

    /// <summary>
    /// Gets the capabilities supported by this provider
    /// </summary>
    EEventBusCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the display name for this provider
    /// </summary>
    string DisplayName { get; }
}
