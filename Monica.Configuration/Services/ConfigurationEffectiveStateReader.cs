using System.Text.Json;
using System.Text.Json.Nodes;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Reads display-safe effective configuration state while sharing one persisted document across a definition snapshot.
/// </summary>
/// <param name="effectiveValueStore">The authoritative effective-value document store.</param>
/// <param name="seedFactory">The factory for persisted and runtime fallback JSON values.</param>
/// <param name="documentEditor">The logical-path reader for effective-value documents.</param>
/// <param name="sourceInspector">The runtime configuration source inspector.</param>
/// <param name="runtimeContext">The current Microsoft configuration runtime context.</param>
public sealed class ConfigurationEffectiveStateReader(
    IConfigurationEffectiveValueStore effectiveValueStore,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    ConfigurationEffectiveValueDocumentEditor documentEditor,
    IConfigurationSourceInspector sourceInspector,
    ConfigurationRuntimeContext runtimeContext)
{
    /// <summary>
    /// Reads one display-safe effective value.
    /// </summary>
    /// <param name="definition">The definition that owns the value.</param>
    /// <param name="logicalPath">The logical path to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The display-safe effective value.</returns>
    public async Task<ConfigurationEffectiveValue> ReadValueAsync(
        ConfigurationDefinition definition,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var document = await effectiveValueStore.GetAsync(definition.DefinitionKey, cancellationToken);
        var publishedValueJson = definition.Origin == ConfigurationDefinitionOrigin.PublishedMetadata
            ? document?.Json ?? seedFactory.CreateSeedJson(definition)
            : null;
        var publishedStoredValue = publishedValueJson is null
            ? null
            : documentEditor.ReadValue(definition, publishedValueJson, logicalPath);
        var targetNode = ConfigurationSchemaNavigator.ResolveNode(definition.Root, logicalPath);
        var runtimeSourceValue = definition.Origin == ConfigurationDefinitionOrigin.LocalScan
                                 && targetNode?.NodeKind == ConfigurationNodeKind.Scalar
            ? sourceInspector.GetSourceChain(definition, logicalPath).Values.FirstOrDefault(value => value.IsEffective)
            : null;
        return ReadValue(
            definition,
            logicalPath,
            targetNode,
            document,
            publishedStoredValue,
            runtimeSourceValue);
    }

    /// <summary>
    /// Reads the effective management state for every directly editable value in one definition.
    /// </summary>
    /// <param name="definition">The definition to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition and its display-safe effective values.</returns>
    public async Task<ConfigurationDefinitionState> ReadDefinitionAsync(
        ConfigurationDefinition definition,
        CancellationToken cancellationToken)
    {
        var document = await effectiveValueStore.GetAsync(definition.DefinitionKey, cancellationToken);
        var publishedValueJson = definition.Origin == ConfigurationDefinitionOrigin.PublishedMetadata
            ? document?.Json ?? seedFactory.CreateSeedJson(definition)
            : null;
        var stateNodes = EnumerateNodes(definition.Root)
            .Where(IsStateValueNode)
            .ToArray();
        var storedValues = publishedValueJson is null
            ? null
            : documentEditor.ReadValues(
                definition,
                publishedValueJson,
                stateNodes.Select(node => node.RelativePath).ToArray());
        var runtimeSourceValues = ReadRuntimeSourceValues(definition, stateNodes);
        var values = new ConfigurationEffectiveValue[stateNodes.Length];
        for (var index = 0; index < stateNodes.Length; index++)
        {
            values[index] = ReadValue(
                definition,
                stateNodes[index].RelativePath,
                stateNodes[index],
                document,
                storedValues?[index],
                runtimeSourceValues?[index]);
        }

        return new ConfigurationDefinitionState
        {
            Definition = definition,
            EffectiveValues = values
        };
    }

    private ConfigurationEffectiveValue ReadValue(
        ConfigurationDefinition definition,
        LogicalPath logicalPath,
        ConfigurationNodeDefinition? targetNode,
        ConfigurationEffectiveValueDocument? document,
        ConfigurationStoredValue? publishedStoredValue,
        ConfigurationSourceValue? runtimeSourceValue)
    {
        var isSensitive = ConfigurationSchemaNavigator.IsSensitivePath(definition.Root, logicalPath);
        var configurationPath = targetNode?.ConfigurationPath
                                ?? ProjectPath(definition, logicalPath);

        if (runtimeSourceValue is not null)
        {
            return new ConfigurationEffectiveValue
            {
                DefinitionKey = definition.DefinitionKey,
                LogicalPath = logicalPath,
                ConfigurationPath = configurationPath,
                DisplayValue = runtimeSourceValue.DisplayValue,
                IsSensitive = runtimeSourceValue.IsSensitive,
                Version = runtimeSourceValue.Source.Kind == ConfigurationSourceKind.MonicaEffectiveStore
                    ? document?.Version
                    : null,
                EffectiveSource = runtimeSourceValue.Source
            };
        }

        if (definition.Origin == ConfigurationDefinitionOrigin.PublishedMetadata)
        {
            return new ConfigurationEffectiveValue
            {
                DefinitionKey = definition.DefinitionKey,
                LogicalPath = logicalPath,
                ConfigurationPath = configurationPath,
                DisplayValue = isSensitive ? null : ToDisplayValue(publishedStoredValue, targetNode),
                IsSensitive = isSensitive,
                Version = document?.Version,
                EffectiveSource = targetNode?.NodeKind == ConfigurationNodeKind.Scalar
                    ? BuildEffectiveStoreSource()
                    : null
            };
        }

        var displayValue = targetNode is null || targetNode.NodeKind == ConfigurationNodeKind.Scalar
            ? runtimeContext.Configuration[configurationPath]
            : seedFactory.CreateRuntimeJson(targetNode, configurationPath);
        return new ConfigurationEffectiveValue
        {
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = logicalPath,
            ConfigurationPath = configurationPath,
            DisplayValue = isSensitive || displayValue is null
                ? null
                : targetNode is null
                    ? displayValue
                    : ConfigurationRegexTextCodec.NormalizeDisplayValue(targetNode, displayValue),
            IsSensitive = isSensitive,
            Version = document?.Version
        };
    }

    private ConfigurationSourceValue?[]? ReadRuntimeSourceValues(
        ConfigurationDefinition definition,
        IReadOnlyList<ConfigurationNodeDefinition> stateNodes)
    {
        if (definition.Origin != ConfigurationDefinitionOrigin.LocalScan)
        {
            return null;
        }

        var scalarNodes = stateNodes
            .Select((node, index) => (Node: node, Index: index))
            .Where(static item => item.Node.NodeKind == ConfigurationNodeKind.Scalar)
            .ToArray();
        var sourceChains = sourceInspector.GetSourceChains(
            definition,
            scalarNodes.Select(static item => item.Node.RelativePath).ToArray());
        var values = new ConfigurationSourceValue?[stateNodes.Count];
        for (var index = 0; index < scalarNodes.Length; index++)
        {
            values[scalarNodes[index].Index] = sourceChains[index].Values.FirstOrDefault(value => value.IsEffective);
        }

        return values;
    }

    private ConfigurationSourceDescriptor BuildEffectiveStoreSource()
    {
        return new ConfigurationSourceDescriptor
        {
            SourceKey = effectiveValueStore.Descriptor.StoreKey,
            DisplayName = effectiveValueStore.Descriptor.DisplayName,
            ProviderType = effectiveValueStore.Descriptor.Kind.ToString(),
            Kind = ConfigurationSourceKind.MonicaEffectiveStore,
            IsManagedByMonica = true,
            IsWritable = effectiveValueStore.Descriptor.IsWritable
        };
    }

    private static string ProjectPath(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(definition.SectionPath))
        {
            parts.Add(definition.SectionPath);
        }

        parts.AddRange(logicalPath.Segments.Select(segment => segment.Value));
        return string.Join(':', parts);
    }

    private static string? ToDisplayValue(ConfigurationStoredValue? storedValue, ConfigurationNodeDefinition? node)
    {
        if (storedValue is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(storedValue.Json);
            if (document.RootElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            if (node?.NodeKind == ConfigurationNodeKind.Scalar)
            {
                var displayValue = document.RootElement.ValueKind == JsonValueKind.String
                    ? document.RootElement.GetString()
                    : document.RootElement.GetRawText();
                return node.IsRegexPatternText
                    ? ConfigurationRegexTextCodec.NormalizePattern(displayValue)
                    : displayValue;
            }

            if (node is not null)
            {
                var normalized = ConfigurationRegexTextCodec.NormalizeJsonNode(
                    node,
                    JsonNode.Parse(document.RootElement.GetRawText()));
                return normalized?.ToJsonString(ConfigurationPersistedJsonOptions.ReadableValue)
                       ?? JsonSerializer.Serialize(document.RootElement, ConfigurationPersistedJsonOptions.ReadableValue);
            }

            return JsonSerializer.Serialize(document.RootElement, ConfigurationPersistedJsonOptions.ReadableValue);
        }
        catch (JsonException)
        {
            return storedValue.Json;
        }
    }

    private static IEnumerable<ConfigurationNodeDefinition> EnumerateNodes(ConfigurationNodeDefinition node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsStateValueNode(ConfigurationNodeDefinition node)
    {
        return node.NodeKind == ConfigurationNodeKind.Scalar
               || node.NodeKind == ConfigurationNodeKind.List
               && node.ListTemplate?.ItemTemplate.NodeKind == ConfigurationNodeKind.Scalar
               || node.NodeKind == ConfigurationNodeKind.Dictionary
               && node.DictionaryTemplate is
               {
                   KeyKind: ConfigurationValueKind.String,
                   ValueTemplate.NodeKind: ConfigurationNodeKind.Scalar
               };
    }
}
