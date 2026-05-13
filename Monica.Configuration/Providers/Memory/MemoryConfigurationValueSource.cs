using System.Collections.Concurrent;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Configuration.Utils;

namespace Monica.Configuration.Providers.Memory;

/// <summary>
/// In-memory writable value source used as the default Phase 1 provider and for tests.
/// </summary>
public sealed class MemoryConfigurationValueSource : IConfigurationValueSource
{
    private readonly ConcurrentDictionary<(string DefinitionKey, string LogicalPath), ConfigurationValueOverride> _values = new();
    private readonly ConfigurationContainerSnapshotEditor _snapshotEditor = new();
    private readonly Lock _mutationLock = new();

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
        return Task.FromResult(ResolveConcurrencyTarget(definitionKey, logicalPath));
    }

    /// <inheritdoc />
    public Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        var request = mutation.Request;

        lock (_mutationLock)
        {
            var existing = ResolveConcurrencyTarget(request);
            EnsureExpectedVersion(request, existing);
            var now = DateTimeOffset.UtcNow;
            var version = (existing?.Version ?? 0) + 1;

            switch (request.MutationKind)
            {
                case ConfigurationMutationKind.Set:
                    ApplySet(mutation, existing, version, now);
                    break;
                case ConfigurationMutationKind.Replace:
                    ApplyReplace(mutation, existing, version, now);
                    break;
                case ConfigurationMutationKind.Remove:
                    ApplyRemove(mutation, existing, version, now);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), request.MutationKind, "Unsupported mutation kind.");
            }

            return Task.FromResult(new ConfigurationMutationResult
            {
                DefinitionKey = request.DefinitionKey,
                LogicalPath = request.LogicalPath,
                NewVersion = version,
                SchemaVersion = EffectiveSchemaVersion(mutation),
                ModifiedTime = now
            });
        }
    }

    private void ApplySet(
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverride? existing,
        long version,
        DateTimeOffset now)
    {
        var request = mutation.Request;
        var container = FindCoveringContainer(request.DefinitionKey, request.LogicalPath);
        if (container is not null)
        {
            var patchedValue = _snapshotEditor.Patch(
                container.Value,
                mutation.Definition,
                container.LogicalPath,
                request.LogicalPath,
                request.Value);
            _values[(container.DefinitionKey, container.LogicalPath.ToCanonicalString())] = CreateOverride(
                mutation with
                {
                    ConfigurationPath = container.ConfigurationPath ?? mutation.ConfigurationPath,
                    Granularity = ConfigurationOverrideGranularity.Container
                },
                container.LogicalPath,
                patchedValue,
                ConfigurationValueState.Active,
                ConfigurationOverrideGranularity.Container,
                version,
                now,
                container.OverrideId);
            return;
        }

        if (mutation.Granularity == ConfigurationOverrideGranularity.Container)
        {
            RemoveDescendants(request.DefinitionKey, request.LogicalPath);
        }

        _values[(request.DefinitionKey, request.LogicalPath.ToCanonicalString())] = CreateOverride(
            mutation,
            request.LogicalPath,
            request.Value,
            ConfigurationValueState.Active,
            mutation.Granularity,
            version,
            now,
            existing?.OverrideId);
    }

    private void ApplyReplace(
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverride? existing,
        long version,
        DateTimeOffset now)
    {
        var request = mutation.Request;
        RemoveDescendants(request.DefinitionKey, request.LogicalPath);
        _values[(request.DefinitionKey, request.LogicalPath.ToCanonicalString())] = CreateOverride(
            mutation,
            request.LogicalPath,
            request.Value,
            ConfigurationValueState.Active,
            ConfigurationOverrideGranularity.Container,
            version,
            now,
            existing?.OverrideId);
    }

    private void ApplyRemove(
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverride? existing,
        long version,
        DateTimeOffset now)
    {
        var request = mutation.Request;
        var key = (request.DefinitionKey, request.LogicalPath.ToCanonicalString());
        var container = FindCoveringContainer(request.DefinitionKey, request.LogicalPath);
        if (container is not null)
        {
            var patchedValue = _snapshotEditor.Remove(
                container.Value,
                mutation.Definition,
                container.LogicalPath,
                request.LogicalPath);
            _values[(container.DefinitionKey, container.LogicalPath.ToCanonicalString())] = CreateOverride(
                mutation with
                {
                    ConfigurationPath = container.ConfigurationPath ?? mutation.ConfigurationPath,
                    Granularity = ConfigurationOverrideGranularity.Container
                },
                container.LogicalPath,
                patchedValue,
                ConfigurationValueState.Active,
                ConfigurationOverrideGranularity.Container,
                version,
                now,
                container.OverrideId);
            return;
        }

        if (mutation.Granularity == ConfigurationOverrideGranularity.Scalar)
        {
            _values.TryRemove(key, out _);
            return;
        }

        RemoveDescendants(request.DefinitionKey, request.LogicalPath);
        _values[key] = CreateOverride(
            mutation,
            request.LogicalPath,
            ConfigurationStoredValue.Null,
            ConfigurationValueState.RemovedSubtree,
            ConfigurationOverrideGranularity.Container,
            version,
            now,
            existing?.OverrideId);
    }

    private ConfigurationValueOverride? ResolveConcurrencyTarget(ConfigurationMutationRequest request)
    {
        return ResolveConcurrencyTarget(request.DefinitionKey, request.LogicalPath);
    }

    private ConfigurationValueOverride? ResolveConcurrencyTarget(string definitionKey, LogicalPath logicalPath)
    {
        return FindCoveringContainer(definitionKey, logicalPath)
               ?? GetStoredValue(definitionKey, logicalPath);
    }

    private ConfigurationValueOverride? FindCoveringContainer(string definitionKey, LogicalPath logicalPath)
    {
        return _values.Values
            .Where(value =>
                string.Equals(value.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
                && value.State == ConfigurationValueState.Active
                && value.Granularity == ConfigurationOverrideGranularity.Container
                && value.LogicalPath != logicalPath
                && ConfigurationPathTokenizer.StartsWith(logicalPath, value.LogicalPath))
            .OrderByDescending(value => value.LogicalPath.Depth)
            .FirstOrDefault();
    }

    private ConfigurationValueOverride? GetStoredValue(string definitionKey, LogicalPath logicalPath)
    {
        _values.TryGetValue((definitionKey, logicalPath.ToCanonicalString()), out var value);
        return value;
    }

    private void EnsureExpectedVersion(ConfigurationMutationRequest request, ConfigurationValueOverride? existing)
    {
        if (request.ExpectedValueVersion is null || existing?.Version == request.ExpectedValueVersion)
        {
            return;
        }

        throw new Exceptions.ConfigurationConcurrencyConflictException(
            $"Expected version {request.ExpectedValueVersion} at '{request.LogicalPath}', but current version is {existing?.Version.ToString() ?? "<none>"}.");
    }

    private void RemoveDescendants(string definitionKey, LogicalPath logicalPath)
    {
        foreach (var key in _values.Keys.Where(key =>
                     string.Equals(key.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
                     && ConfigurationPathTokenizer.StartsWith(LogicalPath.Parse(key.LogicalPath), logicalPath)
                     && key.LogicalPath != logicalPath.ToCanonicalString()).ToArray())
        {
            _values.TryRemove(key, out _);
        }
    }

    private static ConfigurationValueOverride CreateOverride(
        ConfigurationSourceMutation mutation,
        LogicalPath logicalPath,
        ConfigurationStoredValue value,
        ConfigurationValueState state,
        ConfigurationOverrideGranularity granularity,
        long version,
        DateTimeOffset now,
        string? overrideId)
    {
        return new ConfigurationValueOverride
        {
            OverrideId = overrideId ?? Guid.NewGuid().ToString("N"),
            DefinitionKey = mutation.Request.DefinitionKey,
            LogicalPath = logicalPath,
            ConfigurationPath = mutation.ConfigurationPath,
            SourceKey = mutation.SourceKey,
            Granularity = granularity,
            State = state,
            Value = value,
            Version = version,
            SchemaVersion = EffectiveSchemaVersion(mutation),
            LastModifiedTime = now,
            LastModifierId = mutation.Request.Context.ModifierId,
            LastModifierName = mutation.Request.Context.ModifierName
        };
    }

    private static int EffectiveSchemaVersion(ConfigurationSourceMutation mutation)
    {
        return mutation.Request.ExpectedSchemaVersion > 0
            ? mutation.Request.ExpectedSchemaVersion
            : mutation.Definition.SchemaVersion;
    }
}
