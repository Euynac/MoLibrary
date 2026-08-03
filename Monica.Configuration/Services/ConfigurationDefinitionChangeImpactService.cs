using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Builds deterministic change impact from current logical-publisher state.
/// </summary>
internal sealed class ConfigurationDefinitionChangeImpactService(IConfigurationMetadataStore metadataStore)
    : IConfigurationDefinitionChangeImpactService
{
    /// <inheritdoc />
    public async Task<ConfigurationDefinitionChangeImpact> GetImpactAsync(
        IReadOnlyCollection<string> definitionKeys,
        CancellationToken cancellationToken)
    {
        var normalizedDefinitionKeys = NormalizeDefinitionKeys(definitionKeys);
        var statesByDefinition = await metadataStore.GetDefinitionPublisherStatesAsync(
            normalizedDefinitionKeys,
            cancellationToken);

        var affectedDefinitionsByPublisher = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase);
        var canonicalPublisherKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var definitionsWithoutKnownConsumers = new List<string>();

        foreach (var definitionKey in normalizedDefinitionKeys)
        {
            if (!statesByDefinition.TryGetValue(definitionKey, out var states))
            {
                throw new InvalidDataException(
                    $"The configuration metadata store omitted publisher state for requested definition '{definitionKey}'.");
            }

            var participatingStates = states
                .Where(static state =>
                    state.ObservationKind != ConfigurationReloadBehaviorObservationKind.NotConsumed)
                .ToArray();
            if (participatingStates.Length == 0)
            {
                definitionsWithoutKnownConsumers.Add(definitionKey);
                continue;
            }

            foreach (var state in participatingStates)
            {
                if (string.IsNullOrWhiteSpace(state.PublisherKey))
                {
                    throw new InvalidDataException(
                        $"Publisher state for definition '{definitionKey}' has an empty publisher key.");
                }

                var publisherKey = state.PublisherKey.Trim();
                if (!affectedDefinitionsByPublisher.TryGetValue(publisherKey, out var affectedDefinitions))
                {
                    affectedDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    affectedDefinitionsByPublisher[publisherKey] = affectedDefinitions;
                    canonicalPublisherKeys[publisherKey] = publisherKey;
                }
                else if (string.CompareOrdinal(publisherKey, canonicalPublisherKeys[publisherKey]) < 0)
                {
                    canonicalPublisherKeys[publisherKey] = publisherKey;
                }

                affectedDefinitions.Add(definitionKey);
            }
        }

        var affectedPublishers = affectedDefinitionsByPublisher
            .Select(pair => new ConfigurationAffectedPublisher
            {
                PublisherKey = canonicalPublisherKeys[pair.Key],
                DefinitionKeys = pair.Value
                    .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static key => key, StringComparer.Ordinal)
                    .ToArray()
            })
            .OrderBy(static publisher => publisher.PublisherKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static publisher => publisher.PublisherKey, StringComparer.Ordinal)
            .ToArray();

        return new ConfigurationDefinitionChangeImpact
        {
            DefinitionKeys = normalizedDefinitionKeys,
            AffectedPublishers = affectedPublishers,
            DefinitionsWithoutKnownConsumers = definitionsWithoutKnownConsumers
        };
    }

    private static IReadOnlyList<string> NormalizeDefinitionKeys(IReadOnlyCollection<string> definitionKeys)
    {
        ArgumentNullException.ThrowIfNull(definitionKeys);
        var normalized = definitionKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key.Trim())
            .GroupBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static key => key, StringComparer.Ordinal).First())
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "At least one non-empty definition key is required.",
                nameof(definitionKeys));
        }

        return normalized;
    }
}
