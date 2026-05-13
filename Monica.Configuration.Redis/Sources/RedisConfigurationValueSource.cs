using System.Text.Json;
using Microsoft.Extensions.Options;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Configuration.Utils;
using Monica.Modules;
using Monica.StateStore.StackExchange.Connection;
using StackExchange.Redis;

namespace Monica.Configuration.Redis.Sources;

/// <summary>
/// Redis-backed writable Monica configuration override source.
/// </summary>
public sealed class RedisConfigurationValueSource : IConfigurationValueSource, IDisposable
{
    private const string LOCK_KEY = "lock";
    private const string INDEX_KEY = "index";

    private readonly ModuleConfigurationRedisOption _option;
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ConfigurationContainerSnapshotEditor _snapshotEditor = new();

    /// <summary>
    /// Creates a Redis configuration value source.
    /// </summary>
    /// <param name="connectionFactory">Redis connection factory.</param>
    /// <param name="options">Redis configuration options.</param>
    public RedisConfigurationValueSource(
        IRedisConnectionFactory connectionFactory,
        IOptions<ModuleConfigurationRedisOption> options)
    {
        _option = options.Value;
        _connection = connectionFactory.CreateConnection(_option.Redis);
        _database = _connection.GetDatabase(_option.Redis.DatabaseIndex);
    }

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
    public async Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        var indexValues = await _database.SetMembersAsync(Key(INDEX_KEY));
        var overrides = new List<ConfigurationValueOverride>(indexValues.Length);
        foreach (var indexValue in indexValues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!indexValue.HasValue)
            {
                continue;
            }

            var payload = await _database.StringGetAsync(indexValue.ToString());
            if (!payload.HasValue)
            {
                continue;
            }

            var value = Deserialize(payload!);
            if (value is not null)
            {
                overrides.Add(value);
            }
        }

        return overrides;
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        var values = await LoadAsync(cancellationToken);
        return values
            .Where(value =>
                string.Equals(value.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
                && (value.LogicalPath == logicalPath
                    || value is { State: ConfigurationValueState.Active, Granularity: ConfigurationOverrideGranularity.Container }
                    && ConfigurationPathTokenizer.StartsWith(logicalPath, value.LogicalPath)))
            .OrderByDescending(value => value.LogicalPath.Depth)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        var lockToken = Guid.NewGuid().ToString("N");
        var lockKey = Key(LOCK_KEY);
        while (!await _database.LockTakeAsync(lockKey, lockToken, _option.MutationLockTimeout))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(_option.MutationLockRetryInterval, cancellationToken);
        }

        try
        {
            var values = (await LoadAsync(cancellationToken)).ToList();
            var request = mutation.Request;
            var existing = ResolveConcurrencyTarget(values, request.DefinitionKey, request.LogicalPath);
            EnsureExpectedVersion(request, existing);

            var now = DateTimeOffset.UtcNow;
            var version = (existing?.Version ?? 0) + 1;
            var resultValues = request.MutationKind switch
            {
                ConfigurationMutationKind.Set => ApplySet(values, mutation, existing, version, now),
                ConfigurationMutationKind.Replace => ApplyReplace(values, mutation, existing, version, now),
                ConfigurationMutationKind.Remove => ApplyRemove(values, mutation, existing, version, now),
                _ => throw new ArgumentOutOfRangeException(nameof(mutation), request.MutationKind, "Unsupported mutation kind.")
            };

            await ReplaceAllAsync(resultValues, cancellationToken);
            return new ConfigurationMutationResult
            {
                DefinitionKey = request.DefinitionKey,
                LogicalPath = request.LogicalPath,
                NewVersion = version,
                SchemaVersion = EffectiveSchemaVersion(mutation),
                ModifiedTime = now
            };
        }
        finally
        {
            await _database.LockReleaseAsync(lockKey, lockToken);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection.Dispose();
    }

    private IReadOnlyList<ConfigurationValueOverride> ApplySet(
        List<ConfigurationValueOverride> values,
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverride? existing,
        long version,
        DateTimeOffset now)
    {
        var request = mutation.Request;
        var container = FindCoveringContainer(values, request.DefinitionKey, request.LogicalPath);
        if (container is not null)
        {
            var patched = _snapshotEditor.Patch(container.Value, container.LogicalPath, request.LogicalPath, request.Value);
            Upsert(values, CreateOverride(
                mutation with { ConfigurationPath = container.ConfigurationPath ?? mutation.ConfigurationPath },
                container.LogicalPath,
                patched,
                ConfigurationValueState.Active,
                ConfigurationOverrideGranularity.Container,
                version,
                now,
                container.OverrideId));
            return values;
        }

        if (mutation.Granularity == ConfigurationOverrideGranularity.Container)
        {
            RemoveDescendants(values, request.DefinitionKey, request.LogicalPath);
        }

        Upsert(values, CreateOverride(
            mutation,
            request.LogicalPath,
            request.Value,
            ConfigurationValueState.Active,
            mutation.Granularity,
            version,
            now,
            existing?.OverrideId));
        return values;
    }

    private IReadOnlyList<ConfigurationValueOverride> ApplyReplace(
        List<ConfigurationValueOverride> values,
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverride? existing,
        long version,
        DateTimeOffset now)
    {
        var request = mutation.Request;
        RemoveDescendants(values, request.DefinitionKey, request.LogicalPath);
        Upsert(values, CreateOverride(
            mutation,
            request.LogicalPath,
            request.Value,
            ConfigurationValueState.Active,
            ConfigurationOverrideGranularity.Container,
            version,
            now,
            existing?.OverrideId));
        return values;
    }

    private IReadOnlyList<ConfigurationValueOverride> ApplyRemove(
        List<ConfigurationValueOverride> values,
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverride? existing,
        long version,
        DateTimeOffset now)
    {
        var request = mutation.Request;
        var container = FindCoveringContainer(values, request.DefinitionKey, request.LogicalPath);
        if (container is not null)
        {
            var patched = _snapshotEditor.Remove(container.Value, container.LogicalPath, request.LogicalPath);
            Upsert(values, CreateOverride(
                mutation with { ConfigurationPath = container.ConfigurationPath ?? mutation.ConfigurationPath },
                container.LogicalPath,
                patched,
                ConfigurationValueState.Active,
                ConfigurationOverrideGranularity.Container,
                version,
                now,
                container.OverrideId));
            return values;
        }

        if (mutation.Granularity == ConfigurationOverrideGranularity.Scalar)
        {
            values.RemoveAll(value => SamePath(value, request.DefinitionKey, request.LogicalPath));
            return values;
        }

        RemoveDescendants(values, request.DefinitionKey, request.LogicalPath);
        Upsert(values, CreateOverride(
            mutation,
            request.LogicalPath,
            ConfigurationStoredValue.Null,
            ConfigurationValueState.RemovedSubtree,
            ConfigurationOverrideGranularity.Container,
            version,
            now,
            existing?.OverrideId));
        return values;
    }

    private async Task ReplaceAllAsync(IReadOnlyList<ConfigurationValueOverride> values, CancellationToken cancellationToken)
    {
        var existingKeys = await _database.SetMembersAsync(Key(INDEX_KEY));
        if (existingKeys.Length > 0)
        {
            await _database.KeyDeleteAsync(existingKeys.Select(value => (RedisKey)value.ToString()).ToArray());
        }

        await _database.KeyDeleteAsync(Key(INDEX_KEY));
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = OverrideKey(value.DefinitionKey, value.LogicalPath);
            await _database.StringSetAsync(key, Serialize(value));
            await _database.SetAddAsync(Key(INDEX_KEY), key);
        }
    }

    private static ConfigurationValueOverride? ResolveConcurrencyTarget(
        IReadOnlyList<ConfigurationValueOverride> values,
        string definitionKey,
        LogicalPath logicalPath)
    {
        return FindCoveringContainer(values, definitionKey, logicalPath)
               ?? values.FirstOrDefault(value => SamePath(value, definitionKey, logicalPath));
    }

    private static ConfigurationValueOverride? FindCoveringContainer(
        IEnumerable<ConfigurationValueOverride> values,
        string definitionKey,
        LogicalPath logicalPath)
    {
        return values
            .Where(value =>
                string.Equals(value.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
                && value.State == ConfigurationValueState.Active
                && value.Granularity == ConfigurationOverrideGranularity.Container
                && value.LogicalPath != logicalPath
                && ConfigurationPathTokenizer.StartsWith(logicalPath, value.LogicalPath))
            .OrderByDescending(value => value.LogicalPath.Depth)
            .FirstOrDefault();
    }

    private static void EnsureExpectedVersion(ConfigurationMutationRequest request, ConfigurationValueOverride? existing)
    {
        if (request.ExpectedValueVersion is null || existing?.Version == request.ExpectedValueVersion)
        {
            return;
        }

        throw new Exceptions.ConfigurationConcurrencyConflictException(
            $"Expected version {request.ExpectedValueVersion} at '{request.LogicalPath}', but current version is {existing?.Version.ToString() ?? "<none>"}.");
    }

    private static void RemoveDescendants(List<ConfigurationValueOverride> values, string definitionKey, LogicalPath logicalPath)
    {
        values.RemoveAll(value =>
            string.Equals(value.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
            && value.LogicalPath != logicalPath
            && ConfigurationPathTokenizer.StartsWith(value.LogicalPath, logicalPath));
    }

    private static void Upsert(List<ConfigurationValueOverride> values, ConfigurationValueOverride next)
    {
        values.RemoveAll(value => SamePath(value, next.DefinitionKey, next.LogicalPath));
        values.Add(next);
    }

    private static bool SamePath(ConfigurationValueOverride value, string definitionKey, LogicalPath logicalPath)
    {
        return string.Equals(value.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase)
               && value.LogicalPath == logicalPath;
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

    private string OverrideKey(string definitionKey, LogicalPath logicalPath)
    {
        var path = Uri.EscapeDataString(logicalPath.ToCanonicalString());
        return Key($"override:{definitionKey}:{path}");
    }

    private string Key(string suffix)
    {
        return $"{_option.KeyPrefix}:{suffix}";
    }

    private static string Serialize(ConfigurationValueOverride value)
    {
        return JsonSerializer.Serialize(RedisConfigurationOverrideDto.FromOverride(value));
    }

    private static ConfigurationValueOverride? Deserialize(string value)
    {
        return JsonSerializer.Deserialize<RedisConfigurationOverrideDto>(value)?.ToOverride();
    }

    private static int EffectiveSchemaVersion(ConfigurationSourceMutation mutation)
    {
        return mutation.Request.ExpectedSchemaVersion > 0
            ? mutation.Request.ExpectedSchemaVersion
            : mutation.Definition.SchemaVersion;
    }
}

internal sealed record RedisConfigurationOverrideDto
{
    public required string OverrideId { get; init; }

    public required string DefinitionKey { get; init; }

    public required string CanonicalPath { get; init; }

    public string? ConfigurationPath { get; init; }

    public required string SourceKey { get; init; }

    public ConfigurationOverrideGranularity Granularity { get; init; }

    public ConfigurationValueState State { get; init; }

    public required ConfigurationStoredValue Value { get; init; }

    public long Version { get; init; }

    public int SchemaVersion { get; init; }

    public DateTimeOffset LastModifiedTime { get; init; }

    public string? LastModifierId { get; init; }

    public string? LastModifierName { get; init; }

    public static RedisConfigurationOverrideDto FromOverride(ConfigurationValueOverride value)
    {
        return new RedisConfigurationOverrideDto
        {
            OverrideId = value.OverrideId,
            DefinitionKey = value.DefinitionKey,
            CanonicalPath = value.LogicalPath.ToCanonicalString(),
            ConfigurationPath = value.ConfigurationPath,
            SourceKey = value.SourceKey,
            Granularity = value.Granularity,
            State = value.State,
            Value = value.Value,
            Version = value.Version,
            SchemaVersion = value.SchemaVersion,
            LastModifiedTime = value.LastModifiedTime,
            LastModifierId = value.LastModifierId,
            LastModifierName = value.LastModifierName
        };
    }

    public ConfigurationValueOverride ToOverride()
    {
        return new ConfigurationValueOverride
        {
            OverrideId = OverrideId,
            DefinitionKey = DefinitionKey,
            LogicalPath = Models.LogicalPath.Parse(CanonicalPath),
            ConfigurationPath = ConfigurationPath,
            SourceKey = SourceKey,
            Granularity = Granularity,
            State = State,
            Value = Value,
            Version = Version,
            SchemaVersion = SchemaVersion,
            LastModifiedTime = LastModifiedTime,
            LastModifierId = LastModifierId,
            LastModifierName = LastModifierName
        };
    }
}
