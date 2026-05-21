namespace Monica.Configuration.Models;

/// <summary>
/// Describes the active Monica.Configuration storage bundle.
/// </summary>
public sealed record ConfigurationStorageOverview
{
    /// <summary>
    /// Gets the effective value store.
    /// </summary>
    public required ConfigurationStoreDescriptor EffectiveValueStore { get; init; }

    /// <summary>
    /// Gets the history store.
    /// </summary>
    public required ConfigurationStoreDescriptor HistoryStore { get; init; }

    /// <summary>
    /// Gets the metadata store.
    /// </summary>
    public required ConfigurationStoreDescriptor MetadataStore { get; init; }

    /// <summary>
    /// Gets whether a change notifier is registered.
    /// </summary>
    public bool HasChangeNotifier { get; init; }
}
