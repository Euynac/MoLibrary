using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Providers;

namespace Monica.Configuration.Providers.Environment;

/// <summary>
/// Read-only source that maps environment variables into Monica overrides for known scalar leaves.
/// </summary>
public sealed class EnvironmentConfigurationValueSource(IConfigurationDefinitionRegistry definitionRegistry)
    : IConfigurationValueSource
{
    private readonly IConfigurationRoot _environmentConfiguration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "environment:default",
        DisplayName = "Environment",
        Kind = ConfigurationSourceKind.Environment,
        Priority = 10,
        IsWritable = false
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>(
            definitionRegistry.GetAll()
                .SelectMany(definition => ConfigurationSourceNodeEnumerator
                    .EnumerateLeaves(definition)
                    .Select(leaf => ConfigurationSourceNodeEnumerator.ReadLeaf(definition, leaf, _environmentConfiguration, Descriptor.SourceKey))
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
            : ConfigurationSourceNodeEnumerator.ReadLeaf(definition, leaf, _environmentConfiguration, Descriptor.SourceKey));
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Environment configuration source is read-only.");
    }
}
