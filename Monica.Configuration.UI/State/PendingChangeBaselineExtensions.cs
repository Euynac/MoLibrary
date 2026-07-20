using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Support;

namespace Monica.Configuration.UI.State;

internal static class PendingChangeBaselineExtensions
{
    public static PendingChange RebaseBaseline(
        this PendingChange change,
        ConfigurationDefinition definition,
        ConfigurationNodeDefinition schema,
        ConfigurationEffectiveValue effectiveValue,
        long? effectiveStoreVersion,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        var rebased = change with
        {
            DefinitionDisplayName = definition.DisplayName,
            NodeDisplayName = DisplayName(schema),
            OriginalValue = CreateOriginalStoredValue(schema, effectiveValue, localizer),
            OriginalDisplayValue = CreateOriginalDisplayValue(change, schema, effectiveValue),
            ExpectedSchemaVersion = definition.SchemaVersion,
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

        return rebased.WithTarget(definition, effectiveValue.EffectiveSource, effectiveStoreVersion);
    }

    public static bool HasSameOriginalAndNewValue(this PendingChange change)
    {
        if (change.OriginalValue is null)
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(change.OriginalValue.Json), JsonNode.Parse(change.NewValue.Json));
        }
        catch (JsonException)
        {
            return string.Equals(change.OriginalValue.Json, change.NewValue.Json, StringComparison.Ordinal);
        }
    }

    private static ConfigurationStoredValue? CreateOriginalStoredValue(
        ConfigurationNodeDefinition schema,
        ConfigurationEffectiveValue effectiveValue,
        IStringLocalizer<ConfigurationUIResource> localizer)
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
}
