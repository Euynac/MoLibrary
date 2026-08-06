using Monica.StateStore.Abstractions;

namespace Monica.StateStore.UI.Models;

/// <summary>
/// Represents a registered state store provider shown by the dashboard.
/// </summary>
public class StateStoreProviderInfo
{
    /// <summary>
    /// Service key. Null means the non-keyed default provider.
    /// </summary>
    public string? ServiceKey { get; init; }

    /// <summary>
    /// Dashboard-facing service label.
    /// </summary>
    public string DisplayName => ServiceKey ?? "默认";

    /// <summary>
    /// Provider family display name such as Redis or Dapr.
    /// </summary>
    public string ProviderDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Provider type.
    /// </summary>
    public EStateStoreProviderType ProviderType { get; init; }

    /// <summary>
    /// Low-level provider capabilities.
    /// </summary>
    public EStateStoreCapabilities Capabilities { get; init; }

    /// <summary>
    /// Browser features exposed by the dashboard workspace.
    /// </summary>
    public EStateStoreBrowserFeatures BrowserFeatures { get; init; }

    /// <summary>
    /// Preferred search mode for the provider in the dashboard workspace.
    /// </summary>
    public EStateStoreKeySearchMode DefaultSearchMode { get; init; }

    /// <summary>
    /// Whether the provider is distributed.
    /// </summary>
    public bool IsDistributed { get; init; }

    /// <summary>
    /// Whether this provider is the default IStateStore implementation resolved from DI.
    /// </summary>
    public bool IsDefaultStateStore { get; init; }

    /// <summary>
    /// Concrete implementation type name.
    /// </summary>
    public string ImplementationType { get; init; } = "";

    /// <summary>
    /// Whether the provider supports pattern-based key browsing.
    /// </summary>
    public bool SupportsKeyScanning => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.PatternSearch);

    /// <summary>
    /// Whether the provider supports exact key lookup.
    /// </summary>
    public bool SupportsExactLookup => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.ExactLookup);

    /// <summary>
    /// Whether the provider supports value preview.
    /// </summary>
    public bool SupportsValuePreview => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.ValuePreview);

    /// <summary>
    /// Whether the provider supports key creation.
    /// </summary>
    public bool SupportsKeyCreation => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.Create);

    /// <summary>
    /// Whether the provider supports key updates.
    /// </summary>
    public bool SupportsKeyUpdate => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.Update);

    /// <summary>
    /// Whether the provider supports key deletion.
    /// </summary>
    public bool SupportsKeyDeletion => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.Delete);

    /// <summary>
    /// Whether the provider supports bulk delete.
    /// </summary>
    public bool SupportsBulkDelete => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.BulkDelete);

    /// <summary>
    /// Whether the provider supports reading key TTL.
    /// </summary>
    public bool SupportsKeyTTL => BrowserFeatures.HasFlag(EStateStoreBrowserFeatures.TimeToLive);

    /// <summary>
    /// Localized provider type fallback label.
    /// </summary>
    public string ProviderTypeName => ProviderType switch
    {
        EStateStoreProviderType.Memory => "内存缓存",
        EStateStoreProviderType.Redis => "Redis",
        EStateStoreProviderType.Dapr => "Dapr",
        _ => "未知"
    };
}
