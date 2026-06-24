using Monica.Configuration.Models;

namespace Monica.Configuration.Api;

internal sealed partial class ConfigurationApiService
{
    public async Task<ConfigurationParameterListResponse> GetParametersAsync(ConfigurationParameterQuery query)
    {
        var definitions = await LoadDefinitionsAsync(query.DefinitionKey);
        var rows = new List<ConfigurationParameterRow>();
        foreach (var definition in definitions)
        {
            foreach (var node in EnumerateSchemaNodes(definition.Root))
            {
                if (!query.IncludeContainers && node.NodeKind != ConfigurationNodeKind.Scalar)
                {
                    continue;
                }

                ConfigurationEffectiveValue? effectiveValue = null;
                if (node.NodeKind == ConfigurationNodeKind.Scalar && query.IncludeEffectiveValue)
                {
                    effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, node.RelativePath);
                }

                var row = new ConfigurationParameterRow
                {
                    DefinitionKey = definition.DefinitionKey,
                    DefinitionDisplayName = definition.DisplayName,
                    FromProject = definition.FromProject,
                    Category = definition.Category,
                    SchemaVersion = definition.SchemaVersion,
                    SchemaHash = definition.SchemaHash,
                    LogicalPath = node.RelativePath.ToCanonicalString(),
                    ConfigurationPath = effectiveValue?.ConfigurationPath ?? node.ConfigurationPath,
                    NodeKey = node.NodeKey,
                    NodeDisplayName = DisplayName(node),
                    Description = node.Description,
                    NodeKind = node.NodeKind,
                    ValueKind = node.ValueKind,
                    IsNullable = node.IsNullable,
                    IsSensitive = node.IsSensitive,
                    ReloadBehavior = ResolveReloadBehavior(definition, node),
                    DisplayValue = effectiveValue?.DisplayValue,
                    ValueVersion = effectiveValue?.Version,
                    EffectiveSource = query.IncludeSource ? effectiveValue?.EffectiveSource : null
                };

                if (MatchesSearch(row, query.Search))
                {
                    rows.Add(row);
                }
            }
        }

        return new ConfigurationParameterListResponse
        {
            Items = rows
                .OrderBy(static row => row.DefinitionDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static row => row.LogicalPath, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}
