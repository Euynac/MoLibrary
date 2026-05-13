using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Providers.Json;

/// <summary>
/// JSON-backed source placeholder. Full file loading is implemented in Phase 4.
/// </summary>
public sealed class JsonConfigurationValueSource : IConfigurationValueSource
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
        throw new NotSupportedException("JSON configuration source is read-only in the Phase 1 scaffold.");
    }
}
