using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.State;
using Monica.Core.Results;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Refreshes staged UI mutations against the current effective configuration values.
/// </summary>
internal sealed class ConfigurationPendingChangeBaselineService(
    ConfigurationFacade facade,
    IStringLocalizer<ConfigurationUIResource> localizer)
{
    /// <summary>
    /// Updates pending-change original values and optimistic versions from the effective store.
    /// </summary>
    /// <param name="changes">The staged pending changes.</param>
    /// <returns>Pending changes rebased on the current effective values.</returns>
    public async Task<IReadOnlyList<PendingChange>> RefreshAsync(IReadOnlyList<PendingChange> changes)
    {
        if (changes.Count == 0)
        {
            return changes;
        }

        var definitions = new Dictionary<string, ConfigurationDefinition>(StringComparer.OrdinalIgnoreCase);
        var refreshed = new List<PendingChange>(changes.Count);

        foreach (var change in changes)
        {
            var definition = await LoadDefinitionAsync(change.DefinitionKey, definitions);
            if (definition is null)
            {
                refreshed.Add(change);
                continue;
            }

            var schema = ResolveNodeByPath(definition, change.LogicalPath);
            if (schema is null)
            {
                refreshed.Add(change);
                continue;
            }

            var valueResult = await facade.GetEffectiveValueAsync(change.DefinitionKey, change.LogicalPath);
            if (valueResult.IsFailed(out _, out var effectiveValue))
            {
                refreshed.Add(change);
                continue;
            }

            var rebased = Rebase(change, definition, schema, effectiveValue);
            if (!StoredJsonEquals(rebased.OriginalValue, rebased.NewValue))
            {
                refreshed.Add(rebased);
            }
        }

        return refreshed;
    }

    private async Task<ConfigurationDefinition?> LoadDefinitionAsync(
        string definitionKey,
        IDictionary<string, ConfigurationDefinition> definitions)
    {
        if (definitions.TryGetValue(definitionKey, out var cached))
        {
            return cached;
        }

        var result = await facade.GetDefinitionAsync(definitionKey);
        if (result.IsFailed(out _, out var detail))
        {
            return null;
        }

        definitions[definitionKey] = detail.Definition;
        return detail.Definition;
    }

    private PendingChange Rebase(
        PendingChange change,
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        ConfigurationEffectiveValue effectiveValue)
    {
        var originalValue = CreateOriginalStoredValue(schema, effectiveValue);
        var rebased = change with
        {
            DefinitionDisplayName = definition.DisplayName,
            NodeDisplayName = DisplayName(schema),
            OriginalValue = originalValue,
            OriginalDisplayValue = CreateOriginalDisplayValue(change, schema, effectiveValue),
            ExpectedSchemaVersion = definition.SchemaVersion,
            ExpectedValueVersion = effectiveValue.EffectiveSource?.Kind == ConfigurationSourceKind.MonicaEffectiveStore
                ? effectiveValue.Version
                : null,
            TargetKind = ConfigurationMutationTargetKind.MonicaEffectiveStore,
            SourceKey = null,
            SourceDisplayName = null,
            SourceProviderType = null,
            SourcePhysicalPath = null,
            SourceConfigurationPath = null,
            IsSensitive = change.IsSensitive || schema.IsSensitive || effectiveValue.IsSensitive,
            NodeKind = schema.NodeKind,
            ValueKind = schema.ValueKind,
            ReloadBehavior = schema.ResolveEffectiveReloadBehavior(definition)
        };

        return rebased.WithTarget(definition, effectiveValue.EffectiveSource);
    }

    private ConfigurationStoredValue? CreateOriginalStoredValue(
        ConfigurationNodeDefinition schema,
        ConfigurationEffectiveValue effectiveValue)
    {
        if (effectiveValue.DisplayValue is null)
        {
            return null;
        }

        if (schema.NodeKind == ConfigurationNodeKind.Scalar)
        {
            var conversion = ConfigurationScalarValueCodec.ConvertDisplayValue(schema, effectiveValue.DisplayValue, localizer);
            return conversion.StoredValue;
        }

        return TryCreateStoredJson(effectiveValue.DisplayValue);
    }

    private static string? CreateOriginalDisplayValue(
        PendingChange change,
        ConfigurationNodeDefinition schema,
        ConfigurationEffectiveValue effectiveValue)
    {
        if (change.IsSensitive || schema.IsSensitive || effectiveValue.IsSensitive || effectiveValue.DisplayValue is null)
        {
            return null;
        }

        return schema.NodeKind == ConfigurationNodeKind.Scalar
            ? ConfigurationScalarValueCodec.NormalizeDisplayValue(schema, effectiveValue.DisplayValue)
            : effectiveValue.DisplayValue;
    }

    private static string DisplayName(ConfigurationNodeDefinition schema)
    {
        return string.IsNullOrWhiteSpace(schema.DisplayName)
            ? schema.Name
            : schema.DisplayName!;
    }

    private static ConfigurationStoredValue? TryCreateStoredJson(string json)
    {
        try
        {
            return ConfigurationStoredValue.FromJson(JsonNode.Parse(json)?.ToJsonString() ?? "null");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool StoredJsonEquals(ConfigurationStoredValue? originalValue, ConfigurationStoredValue newValue)
    {
        if (originalValue is null)
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(originalValue.Json), JsonNode.Parse(newValue.Json));
        }
        catch (JsonException)
        {
            return string.Equals(originalValue.Json, newValue.Json, StringComparison.Ordinal);
        }
    }

    private static ConfigurationNodeDefinition? ResolveNodeByPath(ConfigurationDefinition definition, LogicalPath path)
    {
        var current = definition.Root;
        foreach (var segment in path.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }
}
