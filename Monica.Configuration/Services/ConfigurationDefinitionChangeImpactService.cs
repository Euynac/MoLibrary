using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

/// <summary>
/// Builds deterministic parameter-level change impact from current definitions and logical-publisher state.
/// </summary>
internal sealed class ConfigurationDefinitionChangeImpactService(
    IConfigurationMetadataStore metadataStore,
    ConfigurationDefinitionResolver definitionResolver)
    : IConfigurationDefinitionChangeImpactService
{
    /// <inheritdoc />
    public async Task<ConfigurationDefinitionChangeImpact> GetImpactAsync(
        IReadOnlyCollection<ConfigurationParameterChangeTarget> targets,
        CancellationToken cancellationToken)
    {
        var resolvedTargets = await ResolveTargetsAsync(targets, cancellationToken);
        var definitionKeys = resolvedTargets
            .Select(static target => target.Definition.DefinitionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var statesByDefinition = await metadataStore.GetDefinitionPublisherStatesAsync(
            definitionKeys,
            cancellationToken);

        var publishers = new Dictionary<string, PublisherImpactBuilder>(StringComparer.OrdinalIgnoreCase);
        var parametersWithoutKnownConsumers = new List<ConfigurationParameterWithoutKnownConsumer>();
        foreach (var target in resolvedTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definitionKey = target.Definition.DefinitionKey;
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
                parametersWithoutKnownConsumers.Add(target.ToParameterWithoutKnownConsumer());
                continue;
            }

            foreach (var state in participatingStates)
            {
                var publisherKey = RequirePublisherKey(definitionKey, state.PublisherKey);
                if (!publishers.TryGetValue(publisherKey, out var publisher))
                {
                    publisher = new PublisherImpactBuilder(publisherKey);
                    publishers[publisherKey] = publisher;
                }
                else
                {
                    publisher.UseCanonicalKey(publisherKey);
                }

                var observation = ResolveParameterObservation(target, state);
                publisher.Parameters.Add(target.ToAffectedParameter(observation));
            }
        }

        return new ConfigurationDefinitionChangeImpact
        {
            AffectedPublishers = publishers.Values
                .Select(static publisher => publisher.Build())
                .OrderBy(static publisher => publisher.PublisherKey, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static publisher => publisher.PublisherKey, StringComparer.Ordinal)
                .ToArray(),
            ParametersWithoutKnownConsumers = parametersWithoutKnownConsumers
        };
    }

    private async Task<IReadOnlyList<ResolvedImpactTarget>> ResolveTargetsAsync(
        IReadOnlyCollection<ConfigurationParameterChangeTarget> targets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
        {
            throw new ArgumentException(
                "At least one configuration parameter target is required.",
                nameof(targets));
        }

        var definitions = await definitionResolver.GetMergedDefinitionsAsync(cancellationToken);
        var definitionsByKey = definitions.ToDictionary(
            static definition => definition.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);
        var resolvedByIdentity = new Dictionary<string, ResolvedImpactTarget>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (target is null)
            {
                throw new ArgumentException("Configuration parameter targets cannot contain null items.", nameof(targets));
            }

            if (string.IsNullOrWhiteSpace(target.DefinitionKey))
            {
                throw new ArgumentException("Configuration parameter definition keys cannot be empty.", nameof(targets));
            }

            if (target.LogicalPath is null)
            {
                throw new ArgumentException("Configuration parameter logical paths cannot be null.", nameof(targets));
            }

            var requestedDefinitionKey = target.DefinitionKey.Trim();
            if (!definitionsByKey.TryGetValue(requestedDefinitionKey, out var definition))
            {
                throw new ConfigurationDefinitionNotFoundException(requestedDefinitionKey);
            }

            var resolution = ConfigurationSchemaNavigator.ResolvePath(definition.Root, target.LogicalPath)
                             ?? throw new ConfigurationValidationFailedException(
                                 $"Logical path '{target.LogicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
            var resolved = new ResolvedImpactTarget(
                definition,
                resolution.CanonicalPath,
                resolution.Node,
                resolution.ReloadBehaviorOverride);
            resolvedByIdentity.TryAdd(resolved.Identity, resolved);
        }

        return resolvedByIdentity.Values
            .OrderBy(static target => target.Definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static target => target.Definition.DefinitionKey, StringComparer.Ordinal)
            .ThenBy(static target => target.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static target => target.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static ConfigurationReloadBehaviorObservation ResolveParameterObservation(
        ResolvedImpactTarget target,
        ConfigurationDefinitionPublisherState publisherState)
    {
        return target.ReloadBehaviorOverride switch
        {
            ConfigurationReloadBehavior.OnlineReloadable =>
                ConfigurationReloadBehaviorObservation.Declared(ConfigurationReloadBehavior.OnlineReloadable),
            ConfigurationReloadBehavior.RequiresRestart =>
                ConfigurationReloadBehaviorObservation.Declared(ConfigurationReloadBehavior.RequiresRestart),
            ConfigurationReloadBehavior.StaticAfterStartup =>
                ConfigurationReloadBehaviorObservation.Declared(ConfigurationReloadBehavior.StaticAfterStartup),
            ConfigurationReloadBehavior.Unknown => ConfigurationReloadBehaviorObservation.Unresolved(),
            null or ConfigurationReloadBehavior.Inherit => new ConfigurationReloadBehaviorObservation(
                publisherState.ObservationKind,
                publisherState.ReloadBehavior),
            _ => throw new InvalidDataException(
                $"Configuration node '{target.Node.NodeKey}' uses unsupported reload behavior "
                + $"'{target.ReloadBehaviorOverride}'.")
        };
    }

    private static string RequirePublisherKey(string definitionKey, string publisherKey)
    {
        if (string.IsNullOrWhiteSpace(publisherKey))
        {
            throw new InvalidDataException(
                $"Publisher state for definition '{definitionKey}' has an empty publisher key.");
        }

        return publisherKey.Trim();
    }

    private sealed record ResolvedImpactTarget(
        ConfigurationDefinition Definition,
        LogicalPath LogicalPath,
        ConfigurationNodeDefinition Node,
        ConfigurationReloadBehavior? ReloadBehaviorOverride)
    {
        internal string Identity =>
            $"{ConfigurationDefinitionIdentity.Compute(Definition.DefinitionKey)}\0{LogicalPath.ToCanonicalString()}";

        internal ConfigurationAffectedParameter ToAffectedParameter(
            ConfigurationReloadBehaviorObservation observation)
        {
            return new ConfigurationAffectedParameter
            {
                DefinitionKey = Definition.DefinitionKey,
                DefinitionDisplayName = Definition.DisplayName,
                LogicalPath = LogicalPath,
                ParameterDisplayName = ResolveParameterDisplayName(),
                ObservationKind = observation.Kind,
                ReloadBehavior = observation.Behavior
            };
        }

        internal ConfigurationParameterWithoutKnownConsumer ToParameterWithoutKnownConsumer()
        {
            return new ConfigurationParameterWithoutKnownConsumer
            {
                DefinitionKey = Definition.DefinitionKey,
                DefinitionDisplayName = Definition.DisplayName,
                LogicalPath = LogicalPath,
                ParameterDisplayName = ResolveParameterDisplayName()
            };
        }

        private string ResolveParameterDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(Node.DisplayName))
            {
                return Node.DisplayName.Trim();
            }

            if (LogicalPath.Depth == 0)
            {
                return Definition.DisplayName;
            }

            return string.IsNullOrWhiteSpace(Node.Name)
                ? LogicalPath.ToCanonicalString()
                : Node.Name;
        }
    }

    private sealed class PublisherImpactBuilder(string publisherKey)
    {
        internal string PublisherKey { get; private set; } = publisherKey;

        internal List<ConfigurationAffectedParameter> Parameters { get; } = [];

        internal void UseCanonicalKey(string candidate)
        {
            if (string.CompareOrdinal(candidate, PublisherKey) < 0)
            {
                PublisherKey = candidate;
            }
        }

        internal ConfigurationAffectedPublisher Build()
        {
            return new ConfigurationAffectedPublisher
            {
                PublisherKey = PublisherKey,
                Parameters = Parameters
                    .OrderBy(static parameter => parameter.DefinitionKey, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static parameter => parameter.DefinitionKey, StringComparer.Ordinal)
                    .ThenBy(static parameter => parameter.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static parameter => parameter.LogicalPath.ToCanonicalString(), StringComparer.Ordinal)
                    .ToArray()
            };
        }
    }
}
