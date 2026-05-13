using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Providers;

namespace Monica.Configuration.Providers.Json;

/// <summary>
/// Read-only source that maps the current JSON-backed Microsoft configuration tree into Monica overrides.
/// </summary>
public sealed class JsonConfigurationValueSource(
    IConfiguration configuration,
    IConfigurationDefinitionRegistry definitionRegistry)
    : IConfigurationValueSource
{
    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "json:default",
        DisplayName = "Json",
        Kind = ConfigurationSourceKind.JsonFile,
        Priority = 1,
        IsWritable = false
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>(
            definitionRegistry.GetAll()
                .SelectMany(definition => ConfigurationSourceNodeEnumerator
                    .EnumerateLeaves(definition)
                    .Select(leaf => ConfigurationSourceNodeEnumerator.ReadLeaf(definition, leaf, configuration, Descriptor.SourceKey))
                    .OfType<ConfigurationValueOverride>())
                .ToArray());
    }

    /// <inheritdoc />
    public Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        if (!definitionRegistry.TryGet(definitionKey, out var definition) || definition is null)
        {
            return Task.FromResult<ConfigurationValueOverride?>(null);
        }

        var leaf = ConfigurationSourceNodeEnumerator
            .EnumerateLeaves(definition)
            .FirstOrDefault(candidate => candidate.LogicalPath == logicalPath);

        return Task.FromResult(leaf is null
            ? null
            : ConfigurationSourceNodeEnumerator.ReadLeaf(definition, leaf, configuration, Descriptor.SourceKey));
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("JSON configuration source is read-only.");
    }
}
