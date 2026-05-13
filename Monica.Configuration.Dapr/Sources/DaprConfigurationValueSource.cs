using Dapr.Client;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Providers;
using Monica.Modules;

namespace Monica.Configuration.Dapr.Sources;

/// <summary>
/// Read-only Dapr Configuration API source mapped through Monica schema leaves.
/// </summary>
public sealed class DaprConfigurationValueSource(
    DaprClient daprClient,
    IConfigurationDefinitionRegistry definitionRegistry,
    IOptions<ModuleConfigurationDaprOption> options)
    : IConfigurationValueSource
{
    private readonly ModuleConfigurationDaprOption _option = options.Value;

    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "dapr:default",
        DisplayName = "Dapr Configuration",
        Kind = ConfigurationSourceKind.DaprConfiguration,
        Priority = 50,
        IsWritable = false,
        SupportsWatch = true
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        var leavesByKey = definitionRegistry.GetAll()
            .SelectMany(definition => ConfigurationSourceNodeEnumerator
                .EnumerateLeaves(definition)
                .Select(leaf => new { definition, leaf }))
            .ToDictionary(item => item.leaf.ConfigurationPath, item => item, StringComparer.OrdinalIgnoreCase);

        if (leavesByKey.Count == 0)
        {
            return [];
        }

        var response = await daprClient.GetConfiguration(
            _option.StoreName,
            leavesByKey.Keys.ToArray(),
            _option.Metadata,
            cancellationToken);

        return response.Items
            .Where(item => leavesByKey.ContainsKey(item.Key))
            .Select(item =>
            {
                var leaf = leavesByKey[item.Key];
                return CreateOverride(leaf.definition, leaf.leaf, item.Value);
            })
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        if (!definitionRegistry.TryGet(definitionKey, out var definition) || definition is null)
        {
            return null;
        }

        var leaf = ConfigurationSourceNodeEnumerator
            .EnumerateLeaves(definition)
            .FirstOrDefault(candidate => candidate.LogicalPath == logicalPath);

        if (leaf is null)
        {
            return null;
        }

        var response = await daprClient.GetConfiguration(
            _option.StoreName,
            [leaf.ConfigurationPath],
            _option.Metadata,
            cancellationToken);

        return response.Items.TryGetValue(leaf.ConfigurationPath, out var item)
            ? CreateOverride(definition, leaf, item)
            : null;
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Dapr configuration source is read-only.");
    }

    private ConfigurationValueOverride CreateOverride(
        ConfigurationDefinition definition,
        ConfigurationSourceLeaf leaf,
        ConfigurationItem item)
    {
        return new ConfigurationValueOverride
        {
            OverrideId = $"{Descriptor.SourceKey}:{definition.DefinitionKey}:{leaf.LogicalPath.ToCanonicalString()}",
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = leaf.LogicalPath,
            ConfigurationPath = leaf.ConfigurationPath,
            SourceKey = Descriptor.SourceKey,
            Granularity = ConfigurationOverrideGranularity.Scalar,
            State = ConfigurationValueState.Active,
            Value = ConfigurationStoredValue.Plain(System.Text.Json.JsonSerializer.Serialize(item.Value)),
            Version = long.TryParse(item.Version, out var version) ? version : 0,
            SchemaVersion = definition.SchemaVersion,
            LastModifiedTime = DateTimeOffset.MinValue
        };
    }
}
