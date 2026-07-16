using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using Monica.Core.Extensions;

namespace Monica.Configuration.Services;

/// <summary>
/// Resolves configuration definitions from local scanner metadata and the published metadata store.
/// </summary>
public sealed class ConfigurationDefinitionResolver(
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationMetadataStore metadataStore)
{
    /// <summary>
    /// Gets a fault-isolated catalog for diagnostic UI scenarios without weakening authoritative resolution.
    /// </summary>
    internal async Task<ConfigurationDefinitionCatalogSnapshot> GetDiagnosticCatalogAsync(
        CancellationToken cancellationToken)
    {
        var entriesByKey = new Dictionary<string, ConfigurationDefinitionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var registeredDefinition in definitionRegistry.GetAll())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = registeredDefinition with { Origin = ConfigurationDefinitionOrigin.LocalScan };
            entriesByKey[definition.DefinitionKey] = new ConfigurationDefinitionCatalogEntry
            {
                Definition = definition,
                Availability = ConfigurationDefinitionAvailability.Available
            };
        }

        IReadOnlyList<ConfigurationPublishedDefinitionEntry> publishedEntries;
        try
        {
            publishedEntries = await metadataStore.ListPublishedDefinitionEntriesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            foreach (var (key, entry) in entriesByKey.ToArray())
            {
                entriesByKey[key] = entry with
                {
                    Availability = ConfigurationDefinitionAvailability.AvailableWithMetadataFault
                };
            }

            return new ConfigurationDefinitionCatalogSnapshot
            {
                Entries = SortCatalogEntries(entriesByKey.Values),
                StoreDiagnostic = CreateStoreDiagnostic(ex)
            };
        }

        foreach (var publishedEntry in publishedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definitionKey = publishedEntry.Metadata.DefinitionKey;
            if (!publishedEntry.IsAvailable)
            {
                if (entriesByKey.TryGetValue(definitionKey, out var localEntry)
                    && localEntry.Definition is not null)
                {
                    entriesByKey[definitionKey] = localEntry with
                    {
                        PublishedMetadata = publishedEntry.Metadata,
                        Diagnostic = publishedEntry.Diagnostic,
                        Availability = ConfigurationDefinitionAvailability.AvailableWithMetadataFault
                    };
                }
                else
                {
                    entriesByKey[definitionKey] = new ConfigurationDefinitionCatalogEntry
                    {
                        PublishedMetadata = publishedEntry.Metadata,
                        Diagnostic = publishedEntry.Diagnostic,
                        Availability = ConfigurationDefinitionAvailability.Unavailable
                    };
                }

                continue;
            }

            var publishedDefinition = publishedEntry.RequireDefinition();
            if (entriesByKey.TryGetValue(definitionKey, out var existing)
                && existing.Definition is { } localDefinition)
            {
                entriesByKey[definitionKey] = new ConfigurationDefinitionCatalogEntry
                {
                    Definition = MergePublishedMetadata(localDefinition, publishedDefinition),
                    PublishedMetadata = publishedEntry.Metadata,
                    Availability = ConfigurationDefinitionAvailability.Available
                };
                continue;
            }

            entriesByKey[definitionKey] = new ConfigurationDefinitionCatalogEntry
            {
                Definition = publishedDefinition with { Origin = ConfigurationDefinitionOrigin.PublishedMetadata },
                PublishedMetadata = publishedEntry.Metadata,
                Availability = ConfigurationDefinitionAvailability.Available
            };
        }

        return new ConfigurationDefinitionCatalogSnapshot
        {
            Entries = SortCatalogEntries(entriesByKey.Values)
        };
    }

    /// <summary>
    /// Gets all locally scanned and published definitions, preferring local metadata on key collisions.
    /// </summary>
    public async Task<IReadOnlyList<ConfigurationDefinition>> GetMergedDefinitionsAsync(CancellationToken cancellationToken)
    {
        var publishedEntries = await metadataStore.ListPublishedDefinitionEntriesAsync(cancellationToken);
        var publishedDefinitions = new List<ConfigurationDefinition>(publishedEntries.Count);
        foreach (var entry in publishedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            publishedDefinitions.Add(entry.RequireDefinition());
        }

        var localDefinitions = definitionRegistry.GetAll()
            .ToArray();
        var definitionsByKey = new Dictionary<string, ConfigurationDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in localDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            definitionsByKey[definition.DefinitionKey] = definition with { Origin = ConfigurationDefinitionOrigin.LocalScan };
        }

        foreach (var published in publishedDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (definitionsByKey.TryGetValue(published.DefinitionKey, out var localDefinition))
            {
                definitionsByKey[published.DefinitionKey] = MergePublishedMetadata(localDefinition, published);
                continue;
            }

            definitionsByKey[published.DefinitionKey] = published with { Origin = ConfigurationDefinitionOrigin.PublishedMetadata };
        }

        return definitionsByKey.Values
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets one local or published definition, preferring local metadata.
    /// </summary>
    public async Task<ConfigurationDefinition> GetRequiredAsync(string definitionKey, CancellationToken cancellationToken)
    {
        if (definitionRegistry.TryGet(definitionKey, out var localDefinition))
        {
            var local = localDefinition!;
            var localPublishedEntry = await metadataStore.GetPublishedDefinitionEntryAsync(
                local.DefinitionKey,
                cancellationToken);
            return MergePublishedMetadata(
                local with { Origin = ConfigurationDefinitionOrigin.LocalScan },
                localPublishedEntry?.RequireDefinition());
        }

        var publishedEntry = await metadataStore.GetPublishedDefinitionEntryAsync(definitionKey, cancellationToken);
        return publishedEntry is null
            ? throw new ConfigurationDefinitionNotFoundException(definitionKey)
            : publishedEntry.RequireDefinition() with { Origin = ConfigurationDefinitionOrigin.PublishedMetadata };
    }

    private static ConfigurationDefinition MergePublishedMetadata(
        ConfigurationDefinition localDefinition,
        ConfigurationDefinition? publishedDefinition)
    {
        if (publishedDefinition is null)
        {
            return localDefinition with { SchemaVersion = Math.Max(localDefinition.SchemaVersion, 1) };
        }

        return localDefinition with
        {
            SchemaVersion = ResolveLocalSchemaVersion(localDefinition, publishedDefinition)
        };
    }

    private static int ResolveLocalSchemaVersion(
        ConfigurationDefinition localDefinition,
        ConfigurationDefinition publishedDefinition)
    {
        var localVersion = Math.Max(localDefinition.SchemaVersion, 1);
        if (!HasSameSchemaHash(localDefinition, publishedDefinition))
        {
            // A differing local schema is the next unpublished revision; reusing the scanner's default version
            // could make historical rows appear compatible with a different sensitivity contract.
            return checked(Math.Max(localVersion, publishedDefinition.SchemaVersion) + 1);
        }

        return Math.Max(localVersion, publishedDefinition.SchemaVersion);
    }

    private static bool HasSameSchemaHash(
        ConfigurationDefinition localDefinition,
        ConfigurationDefinition publishedDefinition)
    {
        return string.Equals(localDefinition.SchemaHash, publishedDefinition.SchemaHash, StringComparison.Ordinal);
    }

    private ConfigurationMetadataStoreDiagnostic CreateStoreDiagnostic(Exception error)
    {
        return new ConfigurationMetadataStoreDiagnostic
        {
            StoreKey = metadataStore.Descriptor.StoreKey,
            StoreDisplayName = metadataStore.Descriptor.DisplayName,
            Kind = ClassifyStoreFailure(error),
            ErrorType = error.GetType().FullName ?? error.GetType().Name,
            Message = error.GetMessageRecursively()
        };
    }

    private static ConfigurationMetadataStoreIssueKind ClassifyStoreFailure(Exception error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case ConfigurationMetadataStoreReadException classified:
                    return classified.Kind;
                case UnauthorizedAccessException:
                    return ConfigurationMetadataStoreIssueKind.AccessDenied;
                case DbException or IOException or TimeoutException:
                    return ConfigurationMetadataStoreIssueKind.Unavailable;
            }
        }

        return ConfigurationMetadataStoreIssueKind.ReadFailed;
    }

    private static IReadOnlyList<ConfigurationDefinitionCatalogEntry> SortCatalogEntries(
        IEnumerable<ConfigurationDefinitionCatalogEntry> entries)
    {
        return entries
            .OrderBy(DisplayNameOrKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string DisplayNameOrKey(ConfigurationDefinitionCatalogEntry entry)
    {
        var displayName = entry.Definition?.DisplayName ?? entry.PublishedMetadata?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName) ? DefinitionKey(entry) : displayName;
    }

    private static string DefinitionKey(ConfigurationDefinitionCatalogEntry entry)
    {
        return entry.Definition?.DefinitionKey ?? entry.PublishedMetadata?.DefinitionKey ?? string.Empty;
    }

    /// <summary>
    /// Attempts to get a locally scanned definition.
    /// </summary>
    public bool TryGetLocal(string definitionKey, [NotNullWhen(true)] out ConfigurationDefinition? definition)
    {
        if (definitionRegistry.TryGet(definitionKey, out var localDefinition) && localDefinition is not null)
        {
            definition = localDefinition with { Origin = ConfigurationDefinitionOrigin.LocalScan };
            return true;
        }

        definition = null;
        return false;
    }
}
