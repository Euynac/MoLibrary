using System.Collections.Concurrent;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Providers.Memory;

/// <summary>
/// In-memory writable value source used as the default Phase 1 provider and for tests.
/// </summary>
public sealed class MemoryConfigurationValueSource : IConfigurationValueSource
{
    private readonly ConcurrentDictionary<(string DefinitionKey, string LogicalPath), ConfigurationValueOverride> _values = new();

    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "memory:default",
        DisplayName = "Memory",
        Kind = ConfigurationSourceKind.Memory,
        Priority = 100,
        IsWritable = true,
        SupportsHistory = false,
        SupportsWatch = false
    };

    /// <inheritdoc />
    public Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ConfigurationValueOverride>>([.. _values.Values]);
    }

    /// <inheritdoc />
    public Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        _values.TryGetValue((definitionKey, logicalPath.ToCanonicalString()), out var value);
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        var key = (request.DefinitionKey, request.LogicalPath.ToCanonicalString());
        var version = _values.TryGetValue(key, out var existing) ? existing.Version + 1 : 1;
        var now = DateTimeOffset.UtcNow;

        if (request.ExpectedValueVersion is not null && existing?.Version != request.ExpectedValueVersion)
        {
            throw new Exceptions.ConfigurationConcurrencyConflictException(
                $"Expected version {request.ExpectedValueVersion} at '{key.Item2}', but current version is {existing?.Version.ToString() ?? "<none>"}.");
        }

        if (request.MutationKind == ConfigurationMutationKind.Remove)
        {
            _values.TryRemove(key, out _);
        }
        else
        {
            _values[key] = new ConfigurationValueOverride
            {
                OverrideId = Guid.NewGuid().ToString("N"),
                DefinitionKey = request.DefinitionKey,
                LogicalPath = request.LogicalPath,
                SourceKey = Descriptor.SourceKey,
                Granularity = ConfigurationOverrideGranularity.Scalar,
                State = ConfigurationValueState.Active,
                Value = request.Value,
                Version = version,
                SchemaVersion = request.ExpectedSchemaVersion,
                LastModifiedTime = now,
                LastModifierId = request.Context.ModifierId,
                LastModifierName = request.Context.ModifierName
            };
        }

        return Task.FromResult(new ConfigurationMutationResult
        {
            DefinitionKey = request.DefinitionKey,
            LogicalPath = request.LogicalPath,
            NewVersion = version,
            SchemaVersion = request.ExpectedSchemaVersion,
            ModifiedTime = now
        });
    }
}
