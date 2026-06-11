using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Resolves configuration definitions from local scanner metadata and the published metadata store.
/// </summary>
public sealed class ConfigurationDefinitionResolver(
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationMetadataStore metadataStore)
{
    private readonly ConcurrentDictionary<string, ConfigurationDefinition> _publishedMetadataByKey =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets all locally scanned and published definitions, preferring local metadata on key collisions.
    /// </summary>
    public async Task<IReadOnlyList<ConfigurationDefinition>> GetMergedDefinitionsAsync(CancellationToken cancellationToken)
    {
        var publishedDefinitions = await metadataStore.ListPublishedDefinitionsAsync(cancellationToken);
        RefreshPublishedMetadataCache(publishedDefinitions);

        var localDefinitions = definitionRegistry.GetAll()
            .ToArray();
        var definitionsByKey = new Dictionary<string, ConfigurationDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in localDefinitions)
        {
            definitionsByKey[definition.DefinitionKey] = definition with { Origin = ConfigurationDefinitionOrigin.LocalScan };
        }

        foreach (var published in publishedDefinitions)
        {
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
            var publishedMetadata = await GetPublishedMetadataAsync(definitionKey, cancellationToken);
            return MergePublishedMetadata(localDefinition! with { Origin = ConfigurationDefinitionOrigin.LocalScan }, publishedMetadata);
        }

        var published = await metadataStore.GetPublishedDefinitionAsync(definitionKey, cancellationToken);
        return published is null
            ? throw new ConfigurationDefinitionNotFoundException(definitionKey)
            : published with { Origin = ConfigurationDefinitionOrigin.PublishedMetadata };
    }

    private async Task<ConfigurationDefinition?> GetPublishedMetadataAsync(
        string definitionKey,
        CancellationToken cancellationToken)
    {
        if (_publishedMetadataByKey.TryGetValue(definitionKey, out var cached))
        {
            return cached;
        }

        var published = await metadataStore.GetPublishedDefinitionAsync(definitionKey, cancellationToken);
        if (published is not null)
        {
            _publishedMetadataByKey[published.DefinitionKey] = published;
        }

        return published;
    }

    private void RefreshPublishedMetadataCache(IReadOnlyList<ConfigurationDefinition> publishedDefinitions)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var published in publishedDefinitions)
        {
            seenKeys.Add(published.DefinitionKey);
            _publishedMetadataByKey[published.DefinitionKey] = published;
        }

        foreach (var key in _publishedMetadataByKey.Keys)
        {
            if (!seenKeys.Contains(key))
            {
                _publishedMetadataByKey.TryRemove(key, out _);
            }
        }
    }

    private static ConfigurationDefinition MergePublishedMetadata(
        ConfigurationDefinition localDefinition,
        ConfigurationDefinition? publishedDefinition)
    {
        if (publishedDefinition is null)
        {
            return localDefinition with { SchemaVersion = Math.Max(localDefinition.SchemaVersion, 1) };
        }

        var schemasMatch = HasSameSchemaHash(localDefinition, publishedDefinition);
        return localDefinition with
        {
            SchemaVersion = ResolveLocalSchemaVersion(localDefinition, publishedDefinition),
            LastSeenTime = schemasMatch ? publishedDefinition.LastSeenTime : null
        };
    }

    private static int ResolveLocalSchemaVersion(
        ConfigurationDefinition localDefinition,
        ConfigurationDefinition publishedDefinition)
    {
        var localVersion = Math.Max(localDefinition.SchemaVersion, 1);
        if (!HasSameSchemaHash(localDefinition, publishedDefinition))
        {
            return localVersion;
        }

        return Math.Max(localVersion, publishedDefinition.SchemaVersion);
    }

    private static bool HasSameSchemaHash(
        ConfigurationDefinition localDefinition,
        ConfigurationDefinition publishedDefinition)
    {
        return string.Equals(localDefinition.SchemaHash, publishedDefinition.SchemaHash, StringComparison.Ordinal);
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
