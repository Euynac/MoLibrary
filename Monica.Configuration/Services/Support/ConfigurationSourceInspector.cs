using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;
using Monica.Modules;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Inspects the active Microsoft configuration root and resolves source chains for Monica definitions.
/// </summary>
internal sealed class ConfigurationSourceInspector(
    IConfiguration configuration,
    IConfigurationDefinitionRegistry definitionRegistry,
    ConfigurationPathProjector pathProjector,
    IOptions<ModuleConfigurationOption> moduleOptions)
    : IConfigurationSourceInspector
{
    private static readonly JsonSerializerOptions READABLE_JSON_OPTIONS = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonDocumentOptions JSON_DOCUMENT_OPTIONS = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Lists all runtime configuration sources.
    /// </summary>
    public IReadOnlyList<ConfigurationSourceDescriptor> GetSources()
    {
        if (configuration is not IConfigurationRoot root)
        {
            return [];
        }

        var managedSources = ManagedJsonConfigurationSourceRegistry.Get(configuration);
        return root.Providers
            .Select((provider, index) => BuildDescriptor(provider, index, managedSources))
            .ToArray();
    }

    /// <summary>
    /// Gets one source descriptor.
    /// </summary>
    public ConfigurationSourceDescriptor GetRequiredSource(string sourceKey)
    {
        return GetSources().FirstOrDefault(source => string.Equals(source.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Configuration source '{sourceKey}' was not found.");
    }

    /// <summary>
    /// Gets all source values for one managed configuration path.
    /// </summary>
    public ConfigurationSourceChain GetSourceChain(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var configurationPath = pathProjector.Project(definition.SectionPath, logicalPath);
        return GetSourceChain(definition, logicalPath, configurationPath);
    }

    /// <summary>
    /// Gets source contribution counts for a configuration definition.
    /// </summary>
    public IReadOnlyList<ConfigurationDefinitionSourceContribution> GetDefinitionContributions(ConfigurationDefinition definition)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return [];
        }

        var sources = GetSources().ToDictionary(source => source.PriorityIndex);
        var counters = new Dictionary<string, ContributionCounter>(StringComparer.OrdinalIgnoreCase);
        var effectivePriorityByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<SourceContributionEntry>();

        foreach (var (provider, index) in root.Providers.Select((provider, index) => (provider, index)))
        {
            if (!sources.TryGetValue(index, out var source))
            {
                continue;
            }

            var suppliedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (configurationPath, _) in EnumerateProviderValues(provider, definition.SectionPath))
            {
                if (!suppliedPaths.Add(configurationPath) || ResolveNodeFromConfigurationPath(definition, configurationPath) is null)
                {
                    continue;
                }

                var counter = GetOrCreateCounter(counters, source);
                counter.SuppliedValueCount++;
                entries.Add(new SourceContributionEntry(source.SourceKey, index, configurationPath));

                if (!effectivePriorityByPath.TryGetValue(configurationPath, out var currentPriority) || index > currentPriority)
                {
                    effectivePriorityByPath[configurationPath] = index;
                }
            }
        }

        foreach (var entry in entries)
        {
            if (effectivePriorityByPath.TryGetValue(entry.ConfigurationPath, out var priority)
                && priority == entry.PriorityIndex
                && counters.TryGetValue(entry.SourceKey, out var counter))
            {
                counter.EffectiveValueCount++;
            }
        }

        return counters.Values
            .OrderByDescending(counter => counter.Source.PriorityIndex)
            .Select(counter => new ConfigurationDefinitionSourceContribution
            {
                Source = counter.Source,
                SuppliedValueCount = counter.SuppliedValueCount,
                EffectiveValueCount = counter.EffectiveValueCount
            })
            .ToArray();
    }

    /// <summary>
    /// Gets all visible configuration values supplied by each runtime source.
    /// </summary>
    public IReadOnlyList<ConfigurationSourceInventory> GetSourceInventories()
    {
        if (configuration is not IConfigurationRoot root)
        {
            return [];
        }

        var definitions = definitionRegistry.GetAll().ToArray();
        var sources = GetSources();
        var sourcesByPriority = sources.ToDictionary(source => source.PriorityIndex);
        var builders = sources.ToDictionary(
            source => source.SourceKey,
            source => new SourceInventoryBuilder(source),
            StringComparer.OrdinalIgnoreCase);
        var entries = new List<SourceInventoryEntry>();
        var effectivePriorityByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var includeUnmanaged = moduleOptions.Value.IncludeUnmanagedSourceInventoryItems;

        foreach (var (provider, index) in root.Providers.Select((provider, index) => (provider, index)))
        {
            if (!sourcesByPriority.TryGetValue(index, out var source))
            {
                continue;
            }

            var suppliedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
            {
                foreach (var (configurationPath, value) in EnumerateProviderValues(provider, definition.SectionPath))
                {
                    if (!suppliedPaths.Add(configurationPath))
                    {
                        continue;
                    }

                    var node = ResolveNodeFromConfigurationPath(definition, configurationPath);
                    var isSensitive = node?.IsSensitive is true;
                    entries.Add(new SourceInventoryEntry(
                        source.SourceKey,
                        index,
                        new ConfigurationSourceInventoryItem
                        {
                            DefinitionKey = definition.DefinitionKey,
                            DefinitionDisplayName = definition.DisplayName,
                            ConfigurationPath = configurationPath,
                            RelativeConfigurationPath = RelativeConfigurationPath(definition.SectionPath, configurationPath),
                            NodeLabel = node?.DisplayName ?? node?.Name,
                            DisplayValue = isSensitive ? null : value,
                            IsSensitive = isSensitive
                        }));

                    if (!effectivePriorityByPath.TryGetValue(configurationPath, out var currentPriority) || index > currentPriority)
                    {
                        effectivePriorityByPath[configurationPath] = index;
                    }
                }
            }

            if (!includeUnmanaged)
            {
                continue;
            }

            foreach (var (configurationPath, value) in EnumerateProviderValues(provider, null))
            {
                if (!suppliedPaths.Add(configurationPath))
                {
                    continue;
                }

                entries.Add(new SourceInventoryEntry(
                    source.SourceKey,
                    index,
                    new ConfigurationSourceInventoryItem
                    {
                        IsManagedByMonica = false,
                        ConfigurationPath = configurationPath,
                        RelativeConfigurationPath = configurationPath,
                        DisplayValue = value
                    }));

                if (!effectivePriorityByPath.TryGetValue(configurationPath, out var currentPriority) || index > currentPriority)
                {
                    effectivePriorityByPath[configurationPath] = index;
                }
            }
        }

        foreach (var entry in entries)
        {
            var isEffective = effectivePriorityByPath.TryGetValue(entry.Item.ConfigurationPath, out var priority)
                              && priority == entry.PriorityIndex;
            builders[entry.SourceKey].Add(entry.Item with { IsEffective = isEffective });
        }

        return builders.Values
            .OrderByDescending(builder => builder.Source.PriorityIndex)
            .Select(builder => builder.Build())
            .ToArray();
    }

    /// <summary>
    /// Gets a display-safe JSON file view.
    /// </summary>
    public async Task<ConfigurationSourceFileView> GetSourceFileViewAsync(string sourceKey, CancellationToken cancellationToken)
    {
        var source = GetRequiredSource(sourceKey);
        if (source.Kind != ConfigurationSourceKind.JsonFile || string.IsNullOrWhiteSpace(source.PhysicalPath))
        {
            throw new InvalidOperationException($"Configuration source '{source.DisplayName}' is not a readable JSON file source.");
        }

        var text = File.Exists(source.PhysicalPath)
            ? await File.ReadAllTextAsync(source.PhysicalPath, cancellationToken)
            : "{}";
        var root = JsonNode.Parse(text, documentOptions: JSON_DOCUMENT_OPTIONS) ?? new JsonObject();
        var redacted = RedactKnownSensitivePaths(root);
        return new ConfigurationSourceFileView
        {
            Source = source,
            Content = root.ToJsonString(READABLE_JSON_OPTIONS),
            HasRedactions = redacted
        };
    }

    private ConfigurationSourceChain GetSourceChain(ConfigurationDefinition definition, LogicalPath logicalPath, string configurationPath)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return new ConfigurationSourceChain
            {
                DefinitionKey = definition.DefinitionKey,
                LogicalPath = logicalPath,
                ConfigurationPath = configurationPath,
                Values = []
            };
        }

        var targetNode = ResolveNode(definition, logicalPath);
        var isSensitive = targetNode?.IsSensitive is true;
        var descriptors = GetSources().ToDictionary(source => source.PriorityIndex);
        var hits = new List<ConfigurationSourceValue>();
        foreach (var (provider, index) in root.Providers.Select((provider, index) => (provider, index)))
        {
            // Chained providers are aliases over another IConfiguration, not leaf sources.
            // Querying them as direct value sources can re-enter the active root and block source-chain reads.
            if (provider is ChainedConfigurationProvider)
            {
                continue;
            }

            var value = BuildSourceValue(provider, descriptors[index], targetNode, configurationPath, isSensitive);
            if (value is null)
            {
                continue;
            }

            hits.Add(value);
        }

        var effectiveIndex = hits.Count == 0 ? -1 : hits.Max(hit => hit.Source.PriorityIndex);
        return new ConfigurationSourceChain
        {
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = logicalPath,
            ConfigurationPath = configurationPath,
            Values = hits
                .Select(hit => hit with { IsEffective = hit.Source.PriorityIndex == effectiveIndex })
                .OrderByDescending(hit => hit.Source.PriorityIndex)
                .ToArray()
        };
    }

    private static ConfigurationSourceValue? BuildSourceValue(
        IConfigurationProvider provider,
        ConfigurationSourceDescriptor source,
        ConfigurationNodeDefinition? targetNode,
        string configurationPath,
        bool isSensitive)
    {
        if (targetNode?.NodeKind == ConfigurationNodeKind.Scalar || targetNode is null)
        {
            return provider.TryGet(configurationPath, out var value)
                ? new ConfigurationSourceValue
                {
                    Source = source,
                    DisplayValue = isSensitive ? null : value,
                    IsSensitive = isSensitive
                }
                : null;
        }

        var node = BuildProviderJsonNode(provider, targetNode, configurationPath);
        if (node is null)
        {
            return null;
        }

        return new ConfigurationSourceValue
        {
            Source = source,
            DisplayValue = node.ToJsonString(READABLE_JSON_OPTIONS),
            IsSensitive = isSensitive
        };
    }

    private static JsonNode? BuildProviderJsonNode(
        IConfigurationProvider provider,
        ConfigurationNodeDefinition schema,
        string configurationPath)
    {
        return schema.NodeKind switch
        {
            ConfigurationNodeKind.Scalar => BuildProviderScalarNode(provider, schema, configurationPath),
            ConfigurationNodeKind.Object => BuildProviderObjectNode(provider, schema, configurationPath),
            ConfigurationNodeKind.Dictionary => BuildProviderDictionaryNode(provider, schema, configurationPath),
            ConfigurationNodeKind.List => BuildProviderListNode(provider, schema, configurationPath),
            _ => null
        };
    }

    private static JsonNode? BuildProviderScalarNode(
        IConfigurationProvider provider,
        ConfigurationNodeDefinition schema,
        string configurationPath)
    {
        if (!provider.TryGet(configurationPath, out var value))
        {
            return null;
        }

        if (schema.IsSensitive)
        {
            return JsonValue.Create("***");
        }

        return schema.ValueKind switch
        {
            ConfigurationValueKind.Boolean when bool.TryParse(value, out var parsed) => JsonValue.Create(parsed),
            ConfigurationValueKind.Integer when long.TryParse(value, out var parsed) => JsonValue.Create(parsed),
            ConfigurationValueKind.Decimal when decimal.TryParse(value, out var parsed) => JsonValue.Create(parsed),
            ConfigurationValueKind.Floating when double.TryParse(value, out var parsed) => JsonValue.Create(parsed),
            _ => JsonValue.Create(value)
        };
    }

    private static JsonNode? BuildProviderObjectNode(
        IConfigurationProvider provider,
        ConfigurationNodeDefinition schema,
        string configurationPath)
    {
        var result = new JsonObject();
        foreach (var child in schema.Children)
        {
            var childPath = string.IsNullOrWhiteSpace(configurationPath)
                ? child.Name
                : $"{configurationPath}:{child.Name}";
            var childNode = BuildProviderJsonNode(provider, child, childPath);
            if (childNode is not null)
            {
                result[child.Name] = childNode;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static JsonNode? BuildProviderDictionaryNode(
        IConfigurationProvider provider,
        ConfigurationNodeDefinition schema,
        string configurationPath)
    {
        if (schema.DictionaryTemplate is null)
        {
            return null;
        }

        var result = new JsonObject();
        foreach (var childKey in GetOrderedProviderChildKeys(provider, configurationPath))
        {
            var childPath = $"{configurationPath}:{childKey}";
            var childNode = BuildProviderJsonNode(provider, schema.DictionaryTemplate.ValueTemplate, childPath);
            if (childNode is not null)
            {
                result[childKey] = childNode;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static JsonNode? BuildProviderListNode(
        IConfigurationProvider provider,
        ConfigurationNodeDefinition schema,
        string configurationPath)
    {
        if (schema.ListTemplate is null)
        {
            return null;
        }

        var result = new JsonArray();
        foreach (var childKey in GetOrderedProviderChildKeys(provider, configurationPath))
        {
            var childPath = $"{configurationPath}:{childKey}";
            var childNode = BuildProviderJsonNode(provider, schema.ListTemplate.ItemTemplate, childPath);
            if (childNode is not null)
            {
                result.Add(childNode);
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static IReadOnlyList<string> GetOrderedProviderChildKeys(IConfigurationProvider provider, string configurationPath)
    {
        return provider.GetChildKeys([], configurationPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => int.TryParse(key, out var index) ? index : int.MaxValue)
            .ThenBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ConfigurationSourceDescriptor BuildDescriptor(
        IConfigurationProvider provider,
        int index,
        IReadOnlyList<ManagedJsonConfigurationSourceRegistration> managedSources)
    {
        if (provider is MonicaConfigurationProvider)
        {
            return new ConfigurationSourceDescriptor
            {
                SourceKey = "monica:effective",
                PriorityIndex = index,
                DisplayName = "Monica Effective Store",
                ProviderType = provider.GetType().Name,
                Kind = ConfigurationSourceKind.MonicaEffectiveStore,
                IsWritable = true,
                Description = "Projected values from Monica.Configuration effective-value store."
            };
        }

        if (provider is JsonConfigurationProvider jsonProvider)
        {
            return BuildJsonDescriptor(jsonProvider, index, managedSources);
        }

        var providerType = provider.GetType();
        var kind = provider switch
        {
            EnvironmentVariablesConfigurationProvider => ConfigurationSourceKind.EnvironmentVariables,
            CommandLineConfigurationProvider => ConfigurationSourceKind.CommandLine,
            MemoryConfigurationProvider => ConfigurationSourceKind.Memory,
            _ => ConfigurationSourceKind.Unsupported
        };

        return new ConfigurationSourceDescriptor
        {
            SourceKey = $"provider:{index}",
            PriorityIndex = index,
            DisplayName = providerType.Name,
            ProviderType = providerType.FullName ?? providerType.Name,
            Kind = kind,
            IsWritable = false,
            ReadOnlyReason = "This provider type is not supported for UI mutation."
        };
    }

    private static ConfigurationSourceDescriptor BuildJsonDescriptor(
        JsonConfigurationProvider provider,
        int index,
        IReadOnlyList<ManagedJsonConfigurationSourceRegistration> managedSources)
    {
        var source = provider.Source;
        var sourcePath = source.Path ?? string.Empty;
        var physicalPath = string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : source.FileProvider?.GetFileInfo(sourcePath)?.PhysicalPath;
        var normalizedPhysicalPath = string.IsNullOrWhiteSpace(physicalPath) ? null : Path.GetFullPath(physicalPath);
        var managed = FindManagedRegistration(sourcePath, normalizedPhysicalPath, managedSources);
        var canWrite = !string.IsNullOrWhiteSpace(normalizedPhysicalPath)
                       && CanWriteJsonFile(normalizedPhysicalPath)
                       && (managed?.IsWritable ?? true);

        return new ConfigurationSourceDescriptor
        {
            SourceKey = normalizedPhysicalPath is null ? $"json:{index}" : $"json:{Hash(normalizedPhysicalPath)}",
            PriorityIndex = index,
            DisplayName = managed?.DisplayName ?? Path.GetFileName(sourcePath),
            ProviderType = provider.GetType().FullName ?? provider.GetType().Name,
            Kind = ConfigurationSourceKind.JsonFile,
            IsManagedByMonica = managed is not null,
            IsWritable = canWrite,
            ReadOnlyReason = canWrite ? null : ResolveJsonReadOnlyReason(normalizedPhysicalPath, managed),
            SourcePath = sourcePath,
            PhysicalPath = normalizedPhysicalPath,
            Optional = source.Optional,
            ReloadOnChange = source.ReloadOnChange,
            Description = managed?.Description
        };
    }

    private static ManagedJsonConfigurationSourceRegistration? FindManagedRegistration(
        string sourcePath,
        string? physicalPath,
        IReadOnlyList<ManagedJsonConfigurationSourceRegistration> registrations)
    {
        return registrations.FirstOrDefault(registration =>
            string.Equals(registration.Path, sourcePath, StringComparison.OrdinalIgnoreCase)
            || physicalPath is not null
            && string.Equals(Path.GetFullPath(registration.Path), physicalPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool CanWriteJsonFile(string physicalPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(physicalPath);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string ResolveJsonReadOnlyReason(string? physicalPath, ManagedJsonConfigurationSourceRegistration? managed)
    {
        if (managed is { IsWritable: false })
        {
            return "This Monica-managed source was registered as read-only.";
        }

        return string.IsNullOrWhiteSpace(physicalPath)
            ? "The JSON provider does not expose a physical file path."
            : "The JSON file directory does not exist or is not writable.";
    }

    private bool RedactKnownSensitivePaths(JsonNode root)
    {
        var redacted = false;
        foreach (var definition in definitionRegistry.GetAll())
        {
            foreach (var node in EnumerateNodes(definition.Root).Where(node => node is { NodeKind: ConfigurationNodeKind.Scalar, IsSensitive: true }))
            {
                var configurationPath = pathProjector.Project(definition.SectionPath, node.RelativePath);
                redacted |= RedactPath(root, configurationPath.Split(':', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return redacted;
    }

    private static bool RedactPath(JsonNode root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
        {
            return false;
        }

        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            current = current switch
            {
                JsonObject jsonObject => jsonObject[segments[i]],
                JsonArray jsonArray when int.TryParse(segments[i], out var index) && index >= 0 && index < jsonArray.Count => jsonArray[index],
                _ => null
            };

            if (current is null)
            {
                return false;
            }
        }

        var last = segments[^1];
        if (current is JsonObject currentObject && currentObject.ContainsKey(last))
        {
            currentObject[last] = "***";
            return true;
        }

        if (current is JsonArray currentArray
            && int.TryParse(last, out var lastIndex)
            && lastIndex >= 0
            && lastIndex < currentArray.Count)
        {
            currentArray[lastIndex] = "***";
            return true;
        }

        return false;
    }

    private static ConfigurationNodeDefinition? ResolveNode(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        return EnumerateNodes(definition.Root).FirstOrDefault(node => node.RelativePath.Equals(logicalPath));
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

    private static IEnumerable<(string ConfigurationPath, string? Value)> EnumerateProviderValues(
        IConfigurationProvider provider,
        string? parentPath)
    {
        var childKeys = provider.GetChildKeys([], parentPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
        foreach (var childKey in childKeys)
        {
            var configurationPath = string.IsNullOrWhiteSpace(parentPath)
                ? childKey
                : $"{parentPath}:{childKey}";

            if (provider.TryGet(configurationPath, out var value))
            {
                yield return (configurationPath, value);
            }

            foreach (var descendant in EnumerateProviderValues(provider, configurationPath))
            {
                yield return descendant;
            }
        }
    }

    private static string RelativeConfigurationPath(string sectionPath, string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return configurationPath;
        }

        return configurationPath.Length <= sectionPath.Length
            ? string.Empty
            : configurationPath[(sectionPath.Length + 1)..];
    }

    private static ConfigurationNodeDefinition? ResolveNodeFromConfigurationPath(
        ConfigurationDefinition definition,
        string configurationPath)
    {
        var sectionSegments = SplitConfigurationPath(definition.SectionPath);
        var pathSegments = SplitConfigurationPath(configurationPath);
        if (pathSegments.Length < sectionSegments.Length || !HasPrefix(pathSegments, sectionSegments))
        {
            return null;
        }

        var current = definition.Root;
        for (var index = sectionSegments.Length; index < pathSegments.Length; index++)
        {
            var segment = pathSegments[index];
            current = current.NodeKind switch
            {
                ConfigurationNodeKind.Object => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase)),
                ConfigurationNodeKind.Dictionary => current.DictionaryTemplate?.ValueTemplate,
                ConfigurationNodeKind.List => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static string[] SplitConfigurationPath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool HasPrefix(IReadOnlyList<string> pathSegments, IReadOnlyList<string> prefixSegments)
    {
        for (var index = 0; index < prefixSegments.Count; index++)
        {
            if (!string.Equals(pathSegments[index], prefixSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    internal static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToUpperInvariant()));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private sealed class ContributionCounter(ConfigurationSourceDescriptor source)
    {
        public ConfigurationSourceDescriptor Source { get; } = source;

        public int SuppliedValueCount { get; set; }

        public int EffectiveValueCount { get; set; }
    }

    private static ContributionCounter GetOrCreateCounter(
        Dictionary<string, ContributionCounter> counters,
        ConfigurationSourceDescriptor source)
    {
        if (counters.TryGetValue(source.SourceKey, out var counter))
        {
            return counter;
        }

        counter = new ContributionCounter(source);
        counters[source.SourceKey] = counter;
        return counter;
    }

    private sealed record SourceContributionEntry(
        string SourceKey,
        int PriorityIndex,
        string ConfigurationPath);

    private sealed class SourceInventoryBuilder(ConfigurationSourceDescriptor source)
    {
        private readonly List<ConfigurationSourceInventoryItem> _items = [];

        public ConfigurationSourceDescriptor Source { get; } = source;

        public void Add(ConfigurationSourceInventoryItem item)
        {
            _items.Add(item);
        }

        public ConfigurationSourceInventory Build()
        {
            return new ConfigurationSourceInventory
            {
                Source = Source,
                SuppliedValueCount = _items.Count,
                EffectiveValueCount = _items.Count(item => item.IsEffective),
                ManagedValueCount = _items.Count(item => item.IsManagedByMonica),
                UnmanagedValueCount = _items.Count(item => !item.IsManagedByMonica),
                Items = _items
                    .OrderBy(item => item.IsManagedByMonica ? 0 : 1)
                    .ThenBy(item => item.DefinitionDisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.RelativeConfigurationPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }
    }

    private sealed record SourceInventoryEntry(
        string SourceKey,
        int PriorityIndex,
        ConfigurationSourceInventoryItem Item);
}
