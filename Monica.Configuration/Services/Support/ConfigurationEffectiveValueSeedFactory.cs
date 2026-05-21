using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Creates first-run effective value documents from host bootstrap configuration and CLR defaults.
/// </summary>
internal sealed class ConfigurationEffectiveValueSeedFactory(IConfiguration configuration)
{
    private static readonly JsonSerializerOptions WRITE_OPTIONS = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates a seed JSON document for a configuration definition.
    /// </summary>
    public string CreateSeedJson(ConfigurationDefinition definition)
    {
        var defaults = CreateDefaultNode(definition) ?? new JsonObject();
        var hostValues = BuildNodeFromConfiguration(definition.Root, definition.SectionPath);
        var merged = hostValues is null ? defaults : Merge(defaults, hostValues);
        return merged.ToJsonString(WRITE_OPTIONS);
    }

    private static JsonNode? CreateDefaultNode(ConfigurationDefinition definition)
    {
        var type = Type.GetType(definition.ClrTypeName, throwOnError: false);
        if (type is null || type.IsAbstract)
        {
            return new JsonObject();
        }

        try
        {
            var instance = Activator.CreateInstance(type);
            return JsonSerializer.SerializeToNode(instance, type, WRITE_OPTIONS) ?? new JsonObject();
        }
        catch (Exception ex) when (ex is MissingMethodException or MemberAccessException or TargetInvocationException)
        {
            return new JsonObject();
        }
    }

    private JsonNode? BuildNodeFromConfiguration(ConfigurationNodeDefinition node, string configurationPath)
    {
        return node.NodeKind switch
        {
            ConfigurationNodeKind.Scalar => BuildScalarNode(node, configurationPath),
            ConfigurationNodeKind.Object => BuildObjectNode(node, configurationPath),
            ConfigurationNodeKind.Dictionary => BuildDictionaryNode(node, configurationPath),
            ConfigurationNodeKind.List => BuildListNode(node, configurationPath),
            _ => null
        };
    }

    private JsonNode? BuildScalarNode(ConfigurationNodeDefinition node, string configurationPath)
    {
        var value = configuration[configurationPath];
        if (value is null)
        {
            return null;
        }

        return node.ValueKind switch
        {
            ConfigurationValueKind.Boolean when bool.TryParse(value, out var parsed) => JsonValue.Create(parsed),
            ConfigurationValueKind.Integer when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => JsonValue.Create(parsed),
            ConfigurationValueKind.Decimal when decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => JsonValue.Create(parsed),
            ConfigurationValueKind.Floating when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => JsonValue.Create(parsed),
            _ => JsonValue.Create(value)
        };
    }

    private JsonNode? BuildObjectNode(ConfigurationNodeDefinition node, string configurationPath)
    {
        var result = new JsonObject();
        foreach (var child in node.Children)
        {
            var childPath = string.IsNullOrWhiteSpace(configurationPath)
                ? child.Name
                : $"{configurationPath}:{child.Name}";
            var childNode = BuildNodeFromConfiguration(child, childPath);
            if (childNode is not null)
            {
                result[child.Name] = childNode;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private JsonNode? BuildDictionaryNode(ConfigurationNodeDefinition node, string configurationPath)
    {
        if (node.DictionaryTemplate is null)
        {
            return null;
        }

        var result = new JsonObject();
        foreach (var child in configuration.GetSection(configurationPath).GetChildren())
        {
            var childNode = BuildNodeFromConfiguration(node.DictionaryTemplate.ValueTemplate, child.Path);
            if (childNode is not null)
            {
                result[child.Key] = childNode;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private JsonNode? BuildListNode(ConfigurationNodeDefinition node, string configurationPath)
    {
        if (node.ListTemplate is null)
        {
            return null;
        }

        var result = new JsonArray();
        foreach (var child in configuration.GetSection(configurationPath).GetChildren())
        {
            var childNode = BuildNodeFromConfiguration(node.ListTemplate.ItemTemplate, child.Path);
            result.Add(childNode);
        }

        return result.Count == 0 ? null : result;
    }

    private static JsonNode Merge(JsonNode target, JsonNode source)
    {
        if (target is not JsonObject targetObject || source is not JsonObject sourceObject)
        {
            return source.DeepClone();
        }

        foreach (var (key, sourceValue) in sourceObject)
        {
            if (sourceValue is JsonObject sourceChild
                && targetObject[key] is JsonObject targetChild)
            {
                targetObject[key] = Merge(targetChild, sourceChild);
                continue;
            }

            targetObject[key] = sourceValue?.DeepClone();
        }

        return targetObject;
    }
}
