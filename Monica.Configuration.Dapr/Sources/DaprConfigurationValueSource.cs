using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Dapr.Sources;

/// <summary>
/// Dapr configuration source placeholder. It is read-only until write semantics are explicitly designed.
/// </summary>
public sealed class DaprConfigurationValueSource : IConfigurationValueSource
{
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
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Dapr configuration source is read-only.");
    }
}
