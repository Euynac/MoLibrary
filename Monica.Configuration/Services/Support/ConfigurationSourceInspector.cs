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
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Projection;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Inspects the active Microsoft configuration root and resolves source chains for Monica definitions.
/// </summary>
internal sealed class ConfigurationSourceInspector(
    IConfiguration configuration,
    IConfigurationDefinitionRegistry definitionRegistry,
    ConfigurationPathProjector pathProjector)
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
        var scalarNodes = EnumerateNodes(definition.Root)
            .Where(node => node.NodeKind == ConfigurationNodeKind.Scalar)
            .ToArray();

        var counters = new Dictionary<string, ContributionCounter>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in scalarNodes)
        {
            var chain = GetSourceChain(definition, node.RelativePath);
            foreach (var value in chain.Values)
            {
                if (!counters.TryGetValue(value.Source.SourceKey, out var counter))
                {
                    counter = new ContributionCounter(value.Source);
                    counters[value.Source.SourceKey] = counter;
                }

                counter.SuppliedValueCount++;
                if (value.IsEffective)
                {
                    counter.EffectiveValueCount++;
                }
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
            if (!provider.TryGet(configurationPath, out var value))
            {
                continue;
            }

            hits.Add(new ConfigurationSourceValue
            {
                Source = descriptors[index],
                DisplayValue = isSensitive ? null : value,
                IsSensitive = isSensitive
            });
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
}
