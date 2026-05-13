using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Providers.Environment;

/// <summary>
/// Environment-backed read-only source placeholder. Full mapping is implemented in Phase 4.
/// </summary>
public sealed class EnvironmentConfigurationValueSource : IConfigurationValueSource
{
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
        return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>([]);
    }

    /// <inheritdoc />
    public Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        return Task.FromResult<ConfigurationValueOverride?>(null);
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Environment configuration source is read-only.");
    }
}
