using System.Text.Json;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationUnifiedVersionMapper
{
    internal static ConfigurationUnifiedVersionEntity ToEntity(ConfigurationUnifiedVersionSummary summary)
    {
        return new ConfigurationUnifiedVersionEntity
        {
            Version = summary.Version,
            MutationGroupId = summary.MutationGroupId,
            TriggerDefinitionKeysJson = JsonSerializer.Serialize(
                summary.TriggerDefinitionKeys,
                ConfigurationPersistedJsonOptions.CompactValue),
            DefinitionKeysJson = JsonSerializer.Serialize(
                summary.DefinitionKeys,
                ConfigurationPersistedJsonOptions.CompactValue),
            DefinitionCount = summary.DefinitionCount,
            CreatedTime = summary.CreatedTime.UtcDateTime,
            ModifierId = summary.ModifierId,
            ModifierName = summary.ModifierName,
            Reason = summary.Reason
        };
    }

    internal static ConfigurationUnifiedVersionSummary ToSummary(ConfigurationUnifiedVersionEntity entity)
    {
        return new ConfigurationUnifiedVersionSummary
        {
            Version = entity.Version,
            MutationGroupId = entity.MutationGroupId,
            TriggerDefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                entity.TriggerDefinitionKeysJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? [],
            DefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                entity.DefinitionKeysJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? [],
            DefinitionCount = entity.DefinitionCount,
            CreatedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(entity.CreatedTime),
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            Reason = entity.Reason
        };
    }

    internal static ConfigurationUnifiedVersionDocumentEntity ToEntity(
        long version,
        ConfigurationUnifiedVersionDefinitionSnapshot definition)
    {
        return new ConfigurationUnifiedVersionDocumentEntity
        {
            Version = version,
            DefinitionIdentity = ConfigurationDefinitionIdentity.Compute(definition.DefinitionKey),
            DefinitionKey = definition.DefinitionKey,
            DisplayName = definition.DisplayName,
            Category = definition.Category,
            FromProject = definition.FromProject,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            EffectiveValueVersion = definition.EffectiveValueVersion,
            Json = ConfigurationPersistenceValueConverter.NormalizeJson(definition.Json),
            SourceContributionsJson = JsonSerializer.Serialize(
                definition.SourceContributions,
                ConfigurationPersistedJsonOptions.CompactValue)
        };
    }

    internal static ConfigurationUnifiedVersionDefinitionSnapshot ToDefinitionSnapshot(
        ConfigurationUnifiedVersionDocumentEntity entity)
    {
        return new ConfigurationUnifiedVersionDefinitionSnapshot
        {
            DefinitionKey = entity.DefinitionKey,
            DisplayName = entity.DisplayName,
            Category = entity.Category,
            FromProject = entity.FromProject,
            SchemaVersion = entity.SchemaVersion,
            SchemaHash = entity.SchemaHash,
            EffectiveValueVersion = entity.EffectiveValueVersion,
            Json = entity.Json,
            SourceContributions = JsonSerializer.Deserialize<
                                      IReadOnlyList<ConfigurationUnifiedVersionSourceContribution>>(
                                      entity.SourceContributionsJson,
                                      ConfigurationPersistedJsonOptions.CompactValue) ?? []
        };
    }

    internal static IReadOnlyList<string> NormalizeKeys(IEnumerable<string> keys)
    {
        return keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
