using Monica.StateStore.Abstractions;
using Monica.Core.Modularity.Diagnostics.Models;

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
    /// Gets the detached module-option diagnostics target associated with this provider, when the provider is owned by
    /// a known StateStore provider module.
    /// </summary>
    public ModuleOptionDiagnosticsTarget? OptionDiagnosticsTarget { get; init; }

    /// <summary>Gets whether this provider has a dedicated module-option diagnostics source.</summary>
    public bool HasOptionDiagnostics => OptionDiagnosticsTarget is not null;

    /// <summary>Gets the stable identity used to rebind provider-detail state.</summary>
    public string UniqueId => $"{ProviderType}:{ServiceKey ?? "default"}:{ImplementationType}";

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

}
