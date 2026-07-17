using System.Text.Json;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationHistoryMapper
{
    internal static ConfigurationDefinitionPublishHistory ToPublishHistory(
        ConfigurationDefinitionPublishHistoryEntity entity)
    {
        return new ConfigurationDefinitionPublishHistory
        {
            HistoryId = entity.HistoryId,
            DefinitionKey = entity.DefinitionKey,
            SectionPath = entity.SectionPath,
            DisplayName = entity.DisplayName,
            Description = entity.Description,
            FromProject = entity.FromProject,
            Category = entity.Category,
            ChangeKind = Enum.Parse<ConfigurationDefinitionPublishChangeKind>(entity.ChangeKind),
            DefinitionRevision = entity.DefinitionRevision,
            PreviousSchemaVersion = entity.PreviousSchemaVersion,
            NewSchemaVersion = entity.NewSchemaVersion,
            PreviousSchemaHash = entity.PreviousSchemaHash,
            NewSchemaHash = entity.NewSchemaHash,
            PreviousSchemaJson = entity.PreviousSchemaJson,
            NewSchemaJson = entity.NewSchemaJson,
            ChangeSummaryJson = entity.ChangeSummaryJson,
            PublisherId = entity.PublisherId,
            PublisherName = entity.PublisherName,
            PublisherVersion = entity.PublisherVersion,
            PublishedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(entity.PublishedTime)
        };
    }

    internal static ConfigurationValueHistoryEntity ToEntity(ConfigurationValueHistory history)
    {
        return new ConfigurationValueHistoryEntity
        {
            HistoryId = history.HistoryId,
            DefinitionIdentity = ConfigurationDefinitionIdentity.Compute(history.DefinitionKey),
            DefinitionKey = history.DefinitionKey,
            LogicalPath = history.LogicalPath.ToCanonicalString(),
            PathDepth = history.LogicalPath.Depth,
            ConfigurationPath = history.ConfigurationPath,
            TargetKind = history.TargetKind.ToString(),
            SourceProviderType = history.SourceProviderType,
            SourceDisplayName = history.SourceDisplayName,
            SourcePhysicalPath = history.SourcePhysicalPath,
            SourceConfigurationPath = history.SourceConfigurationPath,
            MutationKind = history.MutationKind.ToString(),
            Granularity = history.Granularity.ToString(),
            State = history.State.ToString(),
            OldValueJson = history.OldValue is null
                ? null
                : JsonSerializer.Serialize(history.OldValue, ConfigurationPersistedJsonOptions.CompactValue),
            NewValueJson = JsonSerializer.Serialize(history.NewValue, ConfigurationPersistedJsonOptions.CompactValue),
            Version = history.Version,
            SourceRevisionBefore = history.SourceRevisionBefore,
            SourceRevisionAfter = history.SourceRevisionAfter,
            SchemaVersion = history.SchemaVersion,
            SchemaHash = history.SchemaHash,
            ModifiedTime = history.ModifiedTime.UtcDateTime,
            ModifierId = history.ModifierId,
            ModifierName = history.ModifierName,
            Reason = history.Reason,
            MutationGroupId = history.MutationGroupId
        };
    }

    internal static ConfigurationValueHistory ToHistory(ConfigurationValueHistoryEntity entity)
    {
        return new ConfigurationValueHistory
        {
            HistoryId = entity.HistoryId,
            DefinitionKey = entity.DefinitionKey,
            LogicalPath = LogicalPath.Parse(entity.LogicalPath),
            ConfigurationPath = entity.ConfigurationPath,
            TargetKind = string.IsNullOrWhiteSpace(entity.TargetKind)
                ? ConfigurationMutationTargetKind.MonicaEffectiveStore
                : Enum.Parse<ConfigurationMutationTargetKind>(entity.TargetKind),
            SourceProviderType = entity.SourceProviderType,
            SourceDisplayName = entity.SourceDisplayName,
            SourcePhysicalPath = entity.SourcePhysicalPath,
            SourceConfigurationPath = entity.SourceConfigurationPath,
            MutationKind = Enum.Parse<ConfigurationMutationKind>(entity.MutationKind),
            Granularity = Enum.Parse<ConfigurationMutationGranularity>(entity.Granularity),
            State = Enum.Parse<ConfigurationValueState>(entity.State),
            OldValue = JsonToStoredValue(entity.OldValueJson),
            NewValue = JsonToStoredValue(entity.NewValueJson) ?? ConfigurationStoredValue.Null,
            Version = entity.Version,
            SourceRevisionBefore = entity.SourceRevisionBefore,
            SourceRevisionAfter = entity.SourceRevisionAfter,
            SchemaVersion = entity.SchemaVersion,
            SchemaHash = entity.SchemaHash,
            ModifiedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(entity.ModifiedTime),
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            Reason = entity.Reason,
            MutationGroupId = entity.MutationGroupId
        };
    }

    internal static ConfigurationMutationGroup ToGroup(ConfigurationMutationGroupEntity entity)
    {
        return new ConfigurationMutationGroup
        {
            GroupId = entity.GroupId,
            Label = entity.Label,
            Reason = entity.Reason,
            DefinitionKeys = JsonSerializer.Deserialize<IReadOnlyList<string>>(
                entity.DefinitionKeysJson,
                ConfigurationPersistedJsonOptions.CompactValue) ?? [],
            MutationCount = entity.MutationCount,
            CreatedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(entity.CreatedTime),
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            RolledBackTime = entity.RolledBackTime is null
                ? null
                : ConfigurationPersistenceValueConverter.ToUtcOffset(entity.RolledBackTime.Value),
            RolledBackGroupId = entity.RolledBackGroupId,
            Status = Enum.Parse<ConfigurationMutationGroupStatus>(entity.Status)
        };
    }

    internal static ConfigurationMutationGroupEntity ToEntity(ConfigurationMutationGroup group)
    {
        var entity = new ConfigurationMutationGroupEntity
        {
            GroupId = group.GroupId
        };
        ApplyTo(group, entity);
        return entity;
    }

    internal static void ApplyTo(
        ConfigurationMutationGroup group,
        ConfigurationMutationGroupEntity entity)
    {
        entity.Label = group.Label;
        entity.Reason = group.Reason;
        entity.DefinitionKeysJson = JsonSerializer.Serialize(
            group.DefinitionKeys,
            ConfigurationPersistedJsonOptions.CompactValue);
        entity.MutationCount = group.MutationCount;
        entity.CreatedTime = group.CreatedTime.UtcDateTime;
        entity.ModifierId = group.ModifierId;
        entity.ModifierName = group.ModifierName;
        entity.RolledBackTime = group.RolledBackTime?.UtcDateTime;
        entity.RolledBackGroupId = group.RolledBackGroupId;
        entity.Status = group.Status.ToString();
    }

    private static ConfigurationStoredValue? JsonToStoredValue(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ConfigurationStoredValue>(
                json,
                ConfigurationPersistedJsonOptions.CompactValue);
    }
}
