using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Projects merged values into flat Microsoft configuration key/value pairs.
/// </summary>
internal sealed class ConfigurationProjector(
    ConfigurationPathProjector pathProjector,
    ConfigurationStoredValueCodec codec,
    IConfigurationSensitiveValueProtector sensitiveValueProtector)
    : IConfigurationProjector
{
    /// <inheritdoc />
    public IReadOnlyList<ProjectedConfigurationKey> Project(
        IReadOnlyList<ConfigurationDefinition> definitions,
        IReadOnlyList<MergedNodeValue> values)
    {
        var definitionsByKey = definitions.ToDictionary(x => x.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        var listIndexMap = BuildListIndexMap(values);
        return values
            .Where(value => definitionsByKey.ContainsKey(value.DefinitionKey))
            .SelectMany(value =>
            {
                var definition = definitionsByKey[value.DefinitionKey];
                var configurationPath = value.Override.ConfigurationPath is not null && !ContainsListItemKey(value.LogicalPath)
                    ? value.Override.ConfigurationPath
                    : pathProjector.Project(
                        definition.SectionPath,
                        value.LogicalPath,
                        (listPath, itemKey) => ResolveListIndex(listIndexMap, value.DefinitionKey, listPath, itemKey));
                var storedValue = sensitiveValueProtector.Unprotect(value.Override.Value);
                return codec.ToConfigurationValues(configurationPath, storedValue).Select(projected => new ProjectedConfigurationKey
                {
                    Key = projected.Key,
                    Value = projected.Value
                });
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<(string DefinitionKey, string ListPath), IReadOnlyDictionary<string, int>> BuildListIndexMap(
        IReadOnlyList<MergedNodeValue> values)
    {
        return values
            .SelectMany(value => EnumerateListItems(value.DefinitionKey, value.LogicalPath))
            .GroupBy(item => (item.DefinitionKey, ListPath: item.ListPath.ToCanonicalString()))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, int>)group
                    .Select(item => item.ItemKey)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(itemKey => itemKey, StringComparer.Ordinal)
                    .Select((itemKey, index) => new { itemKey, index })
                    .ToDictionary(item => item.itemKey, item => item.index, StringComparer.Ordinal));
    }

    private static IEnumerable<(string DefinitionKey, LogicalPath ListPath, string ItemKey)> EnumerateListItems(
        string definitionKey,
        LogicalPath logicalPath)
    {
        for (var index = 0; index < logicalPath.Segments.Count; index++)
        {
            if (logicalPath.Segments[index] is not ListItemKeySegment itemKey)
            {
                continue;
            }

            yield return (
                definitionKey,
                new LogicalPath(logicalPath.Segments.Take(index).ToArray()),
                itemKey.ItemKey);
        }
    }

    private static bool ContainsListItemKey(LogicalPath logicalPath)
    {
        return logicalPath.Segments.Any(segment => segment is ListItemKeySegment);
    }

    private static int ResolveListIndex(
        IReadOnlyDictionary<(string DefinitionKey, string ListPath), IReadOnlyDictionary<string, int>> listIndexMap,
        string definitionKey,
        LogicalPath listPath,
        string itemKey)
    {
        if (listIndexMap.TryGetValue((definitionKey, listPath.ToCanonicalString()), out var itemIndexes)
            && itemIndexes.TryGetValue(itemKey, out var index))
        {
            return index;
        }

        throw new ConfigurationValidationFailedException(
            $"List item key '{itemKey}' under '{listPath}' could not be resolved to a projected list index.");
    }
}
