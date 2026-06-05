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
    /// <summary>
    /// Gets all locally scanned and published definitions, preferring local metadata on key collisions.
    /// </summary>
    public async Task<IReadOnlyList<ConfigurationDefinition>> GetMergedDefinitionsAsync(CancellationToken cancellationToken)
    {
        var localDefinitions = definitionRegistry.GetAll()
            .Select(definition => definition with { Origin = ConfigurationDefinitionOrigin.LocalScan })
            .ToArray();
        var definitionsByKey = localDefinitions.ToDictionary(
            definition => definition.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);

        foreach (var published in await metadataStore.ListPublishedDefinitionsAsync(cancellationToken))
        {
            definitionsByKey.TryAdd(
                published.DefinitionKey,
                published with { Origin = ConfigurationDefinitionOrigin.PublishedMetadata });
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
            return localDefinition! with { Origin = ConfigurationDefinitionOrigin.LocalScan };
        }

        var published = await metadataStore.GetPublishedDefinitionAsync(definitionKey, cancellationToken);
        return published is null
            ? throw new ConfigurationDefinitionNotFoundException(definitionKey)
            : published with { Origin = ConfigurationDefinitionOrigin.PublishedMetadata };
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
