using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Resolves the effective JSON visible to the current process while preserving published-only definitions from the
/// shared effective-value store.
/// </summary>
internal sealed class ConfigurationEffectiveSnapshotReader(
    IConfigurationEffectiveValueStore effectiveValueStore,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    IConfigurationReloadCoordinator reloadCoordinator)
{
    public async Task<ConfigurationResolvedEffectiveSnapshot> ReadAsync(
        ConfigurationDefinition definition,
        CancellationToken cancellationToken)
    {
        var document = await effectiveValueStore.GetAsync(definition.DefinitionKey, cancellationToken);
        return Resolve(definition, document);
    }

    public async Task<IReadOnlyList<ConfigurationResolvedEffectiveSnapshot>> ReadManyAsync(
        IReadOnlyList<ConfigurationDefinition> definitions,
        CancellationToken cancellationToken)
    {
        if (definitions.Count == 0)
        {
            return [];
        }

        var documents = await effectiveValueStore.GetManyAsync(
            definitions.Select(static definition => definition.DefinitionKey).ToArray(),
            cancellationToken);
        var snapshots = new ConfigurationResolvedEffectiveSnapshot[definitions.Count];
        for (var index = 0; index < definitions.Count; index++)
        {
            snapshots[index] = Resolve(definitions[index], documents[index]);
        }

        return snapshots;
    }

    private ConfigurationResolvedEffectiveSnapshot Resolve(
        ConfigurationDefinition definition,
        ConfigurationEffectiveValueDocument? document)
    {
        if (definition.Origin == ConfigurationDefinitionOrigin.PublishedMetadata)
        {
            if (document is null)
            {
                throw new InvalidOperationException(
                    $"Published configuration definition '{definition.DefinitionKey}' from '{definition.FromProject}' " +
                    "does not have a persisted effective-value document. Start its owning service or initialize the value before using unified version control.");
            }

            return new ConfigurationResolvedEffectiveSnapshot(document.Json, document.Version, document);
        }

        return new ConfigurationResolvedEffectiveSnapshot(
            seedFactory.CreateRuntimeJson(definition.Root, definition.SectionPath),
            reloadCoordinator.GetLoadedMonicaProjectionVersion(definition.DefinitionKey) ?? document?.Version,
            document);
    }
}

/// <summary>
/// Carries one origin-aware effective JSON snapshot and the persisted document observed while resolving it.
/// </summary>
internal sealed record ConfigurationResolvedEffectiveSnapshot(
    string Json,
    long? Version,
    ConfigurationEffectiveValueDocument? PersistedDocument)
{
    public ConfigurationEffectiveValueDocument RequirePersistedDocument(ConfigurationDefinition definition)
    {
        return PersistedDocument
               ?? throw new InvalidOperationException(
                   $"Configuration definition '{definition.DefinitionKey}' has no persisted effective-value document.");
    }

    public long RequireVersion(ConfigurationDefinition definition)
    {
        return Version
               ?? throw new InvalidOperationException(
                   $"Configuration definition '{definition.DefinitionKey}' has no effective-value version.");
    }
}
