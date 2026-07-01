using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.Configuration.Services.Support;

internal sealed class ConfigurationUnifiedVersionSnapshotFactory(
    ConfigurationDefinitionResolver definitionResolver,
    IConfigurationEffectiveValueStore effectiveValueStore,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    IConfigurationSourceInspector sourceInspector,
    IEnumerable<IConfigurationUnifiedVersionFilter> filters,
    IOptions<ModuleConfigurationOption> options)
{
    public bool IsEnabled => options.Value.UnifiedVersionControl.Enabled;

    public async Task<ConfigurationUnifiedVersionCreateRequest?> CreateRequestAsync(
        IReadOnlyList<string> triggerDefinitionKeys,
        string? mutationGroupId,
        ConfigurationMutationContext context,
        DateTimeOffset createdTime,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var selectedDefinitions = await GetSelectedDefinitionsAsync(cancellationToken);
        if (selectedDefinitions.Count == 0)
        {
            return null;
        }

        var normalizedTriggerKeys = triggerDefinitionKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selectedKeys = selectedDefinitions
            .Select(static definition => definition.DefinitionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (normalizedTriggerKeys.Length > 0 && normalizedTriggerKeys.All(key => !selectedKeys.Contains(key)))
        {
            return null;
        }

        var snapshots = new List<ConfigurationUnifiedVersionDefinitionSnapshot>();
        foreach (var definition in selectedDefinitions)
        {
            var document = await effectiveValueStore.GetAsync(definition.DefinitionKey, cancellationToken);
            snapshots.Add(new ConfigurationUnifiedVersionDefinitionSnapshot
            {
                DefinitionKey = definition.DefinitionKey,
                DisplayName = definition.DisplayName,
                Category = definition.Category,
                FromProject = definition.FromProject,
                SchemaVersion = definition.SchemaVersion,
                SchemaHash = definition.SchemaHash,
                EffectiveValueVersion = document?.Version,
                Json = seedFactory.CreateRuntimeJson(definition.Root, definition.SectionPath),
                SourceContributions = CaptureSourceContributions(definition)
            });
        }

        return new ConfigurationUnifiedVersionCreateRequest
        {
            MutationGroupId = mutationGroupId,
            TriggerDefinitionKeys = normalizedTriggerKeys,
            CreatedTime = createdTime,
            ModifierId = context.ModifierId,
            ModifierName = context.ModifierName,
            Reason = context.Reason,
            Definitions = snapshots
        };
    }

    public async Task<IReadOnlyList<ConfigurationDefinition>> GetSelectedDefinitionsAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return [];
        }

        var activeFilters = filters.ToArray();
        if (activeFilters.Length == 0)
        {
            throw new InvalidOperationException(
                "Unified configuration version control is enabled, but no definition filters were registered. " +
                "Call IncludeUnifiedVersionCategories, IncludeUnifiedVersionDefinitions, or UseUnifiedVersionFilter.");
        }

        var definitions = await definitionResolver.GetMergedDefinitionsAsync(cancellationToken);
        return definitions
            .Where(definition => activeFilters.Any(filter => filter.ShouldInclude(definition)))
            .OrderBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<ConfigurationUnifiedVersionSourceContribution> CaptureSourceContributions(
        ConfigurationDefinition definition)
    {
        try
        {
            return sourceInspector.GetDefinitionContributions(definition)
                .Select(contribution => new ConfigurationUnifiedVersionSourceContribution
                {
                    SourceKey = contribution.Source.SourceKey,
                    DisplayName = contribution.Source.DisplayName,
                    Kind = contribution.Source.Kind,
                    SuppliedValueCount = contribution.SuppliedValueCount,
                    EffectiveValueCount = contribution.EffectiveValueCount
                })
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
