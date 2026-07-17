using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationEffectiveValueMapper
{
    internal static ConfigurationEffectiveValueDocument ToDocument(ConfigurationEffectiveValueEntity entity)
    {
        return new ConfigurationEffectiveValueDocument
        {
            DefinitionKey = entity.DefinitionKey,
            Json = entity.Json,
            Version = entity.Version,
            SchemaVersion = entity.SchemaVersion,
            LastModifiedTime = ConfigurationPersistenceValueConverter.ToUtcOffset(entity.LastModifiedTime),
            LastModifierId = entity.LastModifierId,
            LastModifierName = entity.LastModifierName
        };
    }

    internal static Dictionary<string, ConfigurationEffectiveValueEntity> ToEntityDictionary(
        IEnumerable<ConfigurationEffectiveValueEntity> entities)
    {
        var rows = entities.ToArray();
        var duplicate = rows
            .GroupBy(static entity => entity.DefinitionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Skip(1).Any());
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Multiple effective-value rows claim definition key '{duplicate.Key}' ignoring casing.");
        }

        return rows.ToDictionary(
            static entity => entity.DefinitionKey,
            StringComparer.OrdinalIgnoreCase);
    }

    internal static ConfigurationEffectiveValueEntity? GetSingleEntity(
        IReadOnlyList<ConfigurationEffectiveValueEntity> entities,
        string requestedDefinitionKey)
    {
        return entities.Count switch
        {
            0 => null,
            1 => entities[0],
            _ => throw new InvalidOperationException(
                $"Multiple effective-value rows claim definition key '{requestedDefinitionKey}' ignoring casing.")
        };
    }
}
