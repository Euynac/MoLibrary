using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Redis.Sources;

/// <summary>
/// Redis configuration value source placeholder. Phase 4 adds Redis key mapping and serialization.
/// </summary>
public sealed class RedisConfigurationValueSource : IConfigurationValueSource
{
    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "redis:default",
        DisplayName = "Redis",
        Kind = ConfigurationSourceKind.Redis,
        Priority = 150,
        IsWritable = true,
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
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Redis value mutation is reserved for the Redis implementation phase.");
    }
}
