using Monica.Core.Modularity.Abstractions;

namespace Monica.EventBus.Abstractions;

/// <summary>
/// EventBus Provider type enumeration
/// </summary>
public enum EventBusProviderKind
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
public enum EventBusProviderCapabilities
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
public interface IEventBusProviderModule : IModuleProvider
{
    /// <summary>
    /// Gets the type of this EventBus provider
    /// </summary>
    EventBusProviderKind ProviderType { get; }

    /// <summary>
    /// Gets the capabilities supported by this provider
    /// </summary>
    EventBusProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Gets the display name for this provider
    /// </summary>
    string DisplayName { get; }
}
