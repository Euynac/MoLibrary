using Microsoft.EntityFrameworkCore;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Configuration.Utils;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Configuration.EfCore.Sources;

/// <summary>
/// EF Core backed configuration value source.
/// </summary>
public sealed class DatabaseConfigurationValueSource(IDbContextProvider<ConfigurationDbContext> dbContextProvider)
    : IConfigurationValueSource, IConfigurationHistorySource
{
    private readonly ConfigurationContainerSnapshotEditor _snapshotEditor = new();

    /// <inheritdoc />
    public ConfigurationSourceDescriptor Descriptor { get; } = new()
    {
        SourceKey = "db:default",
        DisplayName = "Database",
        Kind = ConfigurationSourceKind.Database,
        Priority = 200,
        IsWritable = true,
        SupportsHistory = true
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueOverride>> LoadAsync(CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entities = await dbContext.ConfigurationValueOverrides
            .AsNoTracking()
            .Where(value => value.SourceKey == Descriptor.SourceKey)
            .ToListAsync(cancellationToken);

        return entities.Select(ToOverride).ToArray();
    }

    /// <inheritdoc />
    public async Task<ConfigurationValueOverride?> GetAsync(string definitionKey, LogicalPath logicalPath, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entities = await dbContext.ConfigurationValueOverrides
            .AsNoTracking()
            .Where(value => value.SourceKey == Descriptor.SourceKey && value.DefinitionKey == definitionKey)
            .ToListAsync(cancellationToken);

        return entities
            .Select(ToOverride)
            .Where(value =>
                value.LogicalPath == logicalPath
                || value is { State: ConfigurationValueState.Active, Granularity: ConfigurationOverrideGranularity.Container }
                && ConfigurationPathTokenizer.StartsWith(logicalPath, value.LogicalPath))
            .OrderByDescending(value => value.LogicalPath.Depth)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> MutateAsync(ConfigurationSourceMutation mutation, CancellationToken cancellationToken)
    {
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var request = mutation.Request;
        var existing = await ResolveConcurrencyTargetAsync(dbContext, request.DefinitionKey, request.LogicalPath, cancellationToken);
        EnsureExpectedVersion(request, existing);

        var now = DateTimeOffset.UtcNow;
        var version = (existing?.Version ?? 0) + 1;
        var result = request.MutationKind switch
        {
            ConfigurationMutationKind.Set => await ApplySetAsync(dbContext, mutation, existing, version, now, cancellationToken),
            ConfigurationMutationKind.Replace => await ApplyReplaceAsync(dbContext, mutation, existing, version, now, cancellationToken),
            ConfigurationMutationKind.Remove => await ApplyRemoveAsync(dbContext, mutation, existing, version, now, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), request.MutationKind, "Unsupported mutation kind.")
        };

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigurationValueHistory>> GetHistoryAsync(
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var canonicalPath = logicalPath.ToCanonicalString();
        var dbContext = await dbContextProvider.GetDbContextAsync();
        var entities = await dbContext.ConfigurationValueHistories
            .AsNoTracking()
            .Where(history =>
                history.SourceKey == Descriptor.SourceKey
                && history.DefinitionKey == definitionKey
                && history.LogicalPath == canonicalPath)
            .OrderByDescending(history => history.ModifiedTime)
            .ThenByDescending(history => history.Version)
            .ToListAsync(cancellationToken);

        return entities.Select(ToHistory).ToArray();
    }

    private async Task<ConfigurationMutationResult> ApplySetAsync(
        ConfigurationDbContext dbContext,
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverrideEntity? existing,
        long version,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var request = mutation.Request;
        var container = await FindCoveringContainerAsync(dbContext, request.DefinitionKey, request.LogicalPath, cancellationToken);
        if (container is not null)
        {
            var containerPath = LogicalPath.Parse(container.LogicalPath);
            var containerMutation = mutation with
            {
                ConfigurationPath = container.ConfigurationPath ?? ProjectConfigurationPath(mutation.Definition.SectionPath, containerPath)
            };
            var oldContainerValue = ToStoredValue(container);
            var patchedValue = _snapshotEditor.Patch(
                oldContainerValue,
                mutation.Definition,
                containerPath,
                request.LogicalPath,
                request.Value);
            UpsertOverride(container, containerMutation, containerPath, patchedValue, ConfigurationValueState.Active, ConfigurationOverrideGranularity.Container, version, now);
            AddHistory(dbContext, containerMutation, containerPath, ConfigurationMutationKind.Set, patchedValue, oldContainerValue, ConfigurationValueState.Active, ConfigurationOverrideGranularity.Container, version, now);
            return CreateResult(mutation, version, now);
        }

        if (mutation.Granularity == ConfigurationOverrideGranularity.Container)
        {
            await RemoveDescendantsAsync(dbContext, request.DefinitionKey, request.LogicalPath, cancellationToken);
        }

        var target = existing ?? CreateEntity(request.DefinitionKey, request.LogicalPath);
        if (existing is null)
        {
            dbContext.ConfigurationValueOverrides.Add(target);
        }

        var oldValue = existing is null ? null : ToStoredValue(existing);
        UpsertOverride(target, mutation, request.LogicalPath, request.Value, ConfigurationValueState.Active, mutation.Granularity, version, now);
        AddHistory(dbContext, mutation, request.LogicalPath, ConfigurationMutationKind.Set, request.Value, oldValue, ConfigurationValueState.Active, mutation.Granularity, version, now);
        return CreateResult(mutation, version, now);
    }

    private async Task<ConfigurationMutationResult> ApplyReplaceAsync(
        ConfigurationDbContext dbContext,
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverrideEntity? existing,
        long version,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var request = mutation.Request;
        await RemoveDescendantsAsync(dbContext, request.DefinitionKey, request.LogicalPath, cancellationToken);

        var target = existing ?? CreateEntity(request.DefinitionKey, request.LogicalPath);
        if (existing is null)
        {
            dbContext.ConfigurationValueOverrides.Add(target);
        }

        var oldValue = existing is null ? null : ToStoredValue(existing);
        UpsertOverride(target, mutation, request.LogicalPath, request.Value, ConfigurationValueState.Active, ConfigurationOverrideGranularity.Container, version, now);
        AddHistory(dbContext, mutation, request.LogicalPath, ConfigurationMutationKind.Replace, request.Value, oldValue, ConfigurationValueState.Active, ConfigurationOverrideGranularity.Container, version, now);
        return CreateResult(mutation, version, now);
    }

    private async Task<ConfigurationMutationResult> ApplyRemoveAsync(
        ConfigurationDbContext dbContext,
        ConfigurationSourceMutation mutation,
        ConfigurationValueOverrideEntity? existing,
        long version,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var request = mutation.Request;
        var container = await FindCoveringContainerAsync(dbContext, request.DefinitionKey, request.LogicalPath, cancellationToken);
        if (container is not null)
        {
            var containerPath = LogicalPath.Parse(container.LogicalPath);
            var containerMutation = mutation with
            {
                ConfigurationPath = container.ConfigurationPath ?? ProjectConfigurationPath(mutation.Definition.SectionPath, containerPath)
            };
            var oldValue = ToStoredValue(container);
            var patchedValue = _snapshotEditor.Remove(
                oldValue,
                mutation.Definition,
                containerPath,
                request.LogicalPath);
            UpsertOverride(container, containerMutation, containerPath, patchedValue, ConfigurationValueState.Active, ConfigurationOverrideGranularity.Container, version, now);
            AddHistory(dbContext, containerMutation, containerPath, ConfigurationMutationKind.Remove, patchedValue, oldValue, ConfigurationValueState.Active, ConfigurationOverrideGranularity.Container, version, now);
            return CreateResult(mutation, version, now);
        }

        var oldExactValue = existing is null ? null : ToStoredValue(existing);
        if (mutation.Granularity == ConfigurationOverrideGranularity.Scalar)
        {
            if (existing is not null)
            {
                dbContext.ConfigurationValueOverrides.Remove(existing);
            }

            AddHistory(dbContext, mutation, request.LogicalPath, ConfigurationMutationKind.Remove, ConfigurationStoredValue.Null, oldExactValue, ConfigurationValueState.RemovedOverride, ConfigurationOverrideGranularity.Scalar, version, now);
            return CreateResult(mutation, version, now);
        }

        await RemoveDescendantsAsync(dbContext, request.DefinitionKey, request.LogicalPath, cancellationToken);
        var target = existing ?? CreateEntity(request.DefinitionKey, request.LogicalPath);
        if (existing is null)
        {
            dbContext.ConfigurationValueOverrides.Add(target);
        }

        UpsertOverride(target, mutation, request.LogicalPath, ConfigurationStoredValue.Null, ConfigurationValueState.RemovedSubtree, ConfigurationOverrideGranularity.Container, version, now);
        AddHistory(dbContext, mutation, request.LogicalPath, ConfigurationMutationKind.Remove, ConfigurationStoredValue.Null, oldExactValue, ConfigurationValueState.RemovedSubtree, ConfigurationOverrideGranularity.Container, version, now);
        return CreateResult(mutation, version, now);
    }

    private async Task<ConfigurationValueOverrideEntity?> ResolveConcurrencyTargetAsync(
        ConfigurationDbContext dbContext,
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        return await FindCoveringContainerAsync(dbContext, definitionKey, logicalPath, cancellationToken)
               ?? await FindExactAsync(dbContext, definitionKey, logicalPath, cancellationToken);
    }

    private async Task<ConfigurationValueOverrideEntity?> FindExactAsync(
        ConfigurationDbContext dbContext,
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var canonicalPath = logicalPath.ToCanonicalString();
        return await dbContext.ConfigurationValueOverrides
            .FirstOrDefaultAsync(value =>
                value.SourceKey == Descriptor.SourceKey
                && value.DefinitionKey == definitionKey
                && value.LogicalPath == canonicalPath,
                cancellationToken);
    }

    private async Task<ConfigurationValueOverrideEntity?> FindCoveringContainerAsync(
        ConfigurationDbContext dbContext,
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var entities = await dbContext.ConfigurationValueOverrides
            .Where(value =>
                value.SourceKey == Descriptor.SourceKey
                && value.DefinitionKey == definitionKey
                && value.State == ConfigurationValueState.Active.ToString()
                && value.Granularity == ConfigurationOverrideGranularity.Container.ToString()
                && value.PathDepth < logicalPath.Depth)
            .ToListAsync(cancellationToken);

        return entities
            .Where(value => ConfigurationPathTokenizer.StartsWith(logicalPath, LogicalPath.Parse(value.LogicalPath)))
            .OrderByDescending(value => value.PathDepth)
            .FirstOrDefault();
    }

    private async Task RemoveDescendantsAsync(
        ConfigurationDbContext dbContext,
        string definitionKey,
        LogicalPath logicalPath,
        CancellationToken cancellationToken)
    {
        var entities = await dbContext.ConfigurationValueOverrides
            .Where(value =>
                value.SourceKey == Descriptor.SourceKey
                && value.DefinitionKey == definitionKey
                && value.PathDepth > logicalPath.Depth)
            .ToListAsync(cancellationToken);

        dbContext.ConfigurationValueOverrides.RemoveRange(
            entities.Where(value => ConfigurationPathTokenizer.StartsWith(LogicalPath.Parse(value.LogicalPath), logicalPath)));
    }

    private static void EnsureExpectedVersion(ConfigurationMutationRequest request, ConfigurationValueOverrideEntity? existing)
    {
        if (request.ExpectedValueVersion is null || existing?.Version == request.ExpectedValueVersion)
        {
            return;
        }

        throw new Exceptions.ConfigurationConcurrencyConflictException(
            $"Expected version {request.ExpectedValueVersion} at '{request.LogicalPath}', but current version is {existing?.Version.ToString() ?? "<none>"}.");
    }

    private static ConfigurationValueOverrideEntity CreateEntity(string definitionKey, LogicalPath logicalPath)
    {
        return new ConfigurationValueOverrideEntity
        {
            OverrideId = Guid.NewGuid().ToString("N"),
            DefinitionKey = definitionKey,
            LogicalPath = logicalPath.ToCanonicalString(),
            PathDepth = logicalPath.Depth
        };
    }

    private void UpsertOverride(
        ConfigurationValueOverrideEntity entity,
        ConfigurationSourceMutation mutation,
        LogicalPath logicalPath,
        ConfigurationStoredValue value,
        ConfigurationValueState state,
        ConfigurationOverrideGranularity granularity,
        long version,
        DateTimeOffset now)
    {
        entity.DefinitionKey = mutation.Request.DefinitionKey;
        entity.LogicalPath = logicalPath.ToCanonicalString();
        entity.PathDepth = logicalPath.Depth;
        entity.ConfigurationPath = mutation.ConfigurationPath;
        entity.SourceKey = Descriptor.SourceKey;
        entity.Granularity = granularity.ToString();
        entity.State = state.ToString();
        entity.StoredValueKind = value.Kind.ToString();
        entity.PlainJson = value.PlainJson;
        entity.ProtectedPayload = value.ProtectedPayload;
        entity.SecretReference = value.SecretReference;
        entity.Version = version;
        entity.SchemaVersion = EffectiveSchemaVersion(mutation);
        entity.LastModifiedTime = now;
        entity.LastModifierId = mutation.Request.Context.ModifierId;
        entity.LastModifierName = mutation.Request.Context.ModifierName;
    }

    private void AddHistory(
        ConfigurationDbContext dbContext,
        ConfigurationSourceMutation mutation,
        LogicalPath logicalPath,
        ConfigurationMutationKind mutationKind,
        ConfigurationStoredValue newValue,
        ConfigurationStoredValue? oldValue,
        ConfigurationValueState state,
        ConfigurationOverrideGranularity granularity,
        long version,
        DateTimeOffset now)
    {
        dbContext.ConfigurationValueHistories.Add(new ConfigurationValueHistoryEntity
        {
            HistoryId = Guid.NewGuid().ToString("N"),
            DefinitionKey = mutation.Request.DefinitionKey,
            LogicalPath = logicalPath.ToCanonicalString(),
            PathDepth = logicalPath.Depth,
            ConfigurationPath = mutation.ConfigurationPath,
            SourceKey = Descriptor.SourceKey,
            MutationKind = mutationKind.ToString(),
            Granularity = granularity.ToString(),
            State = state.ToString(),
            OldValueJson = oldValue is null ? null : StoredValueToJson(oldValue),
            NewValueJson = StoredValueToJson(newValue),
            Version = version,
            SchemaVersion = EffectiveSchemaVersion(mutation),
            ModifiedTime = now,
            ModifierId = mutation.Request.Context.ModifierId,
            ModifierName = mutation.Request.Context.ModifierName,
            Reason = mutation.Request.Context.Reason
        });
    }

    private static ConfigurationValueOverride ToOverride(ConfigurationValueOverrideEntity entity)
    {
        return new ConfigurationValueOverride
        {
            OverrideId = entity.OverrideId,
            DefinitionKey = entity.DefinitionKey,
            LogicalPath = LogicalPath.Parse(entity.LogicalPath),
            ConfigurationPath = entity.ConfigurationPath,
            SourceKey = entity.SourceKey,
            Granularity = Enum.Parse<ConfigurationOverrideGranularity>(entity.Granularity),
            State = Enum.Parse<ConfigurationValueState>(entity.State),
            Value = ToStoredValue(entity),
            Version = entity.Version,
            SchemaVersion = entity.SchemaVersion,
            LastModifiedTime = entity.LastModifiedTime,
            LastModifierId = entity.LastModifierId,
            LastModifierName = entity.LastModifierName
        };
    }

    private static ConfigurationValueHistory ToHistory(ConfigurationValueHistoryEntity entity)
    {
        return new ConfigurationValueHistory
        {
            HistoryId = entity.HistoryId,
            DefinitionKey = entity.DefinitionKey,
            LogicalPath = LogicalPath.Parse(entity.LogicalPath),
            ConfigurationPath = entity.ConfigurationPath,
            SourceKey = entity.SourceKey,
            MutationKind = Enum.Parse<ConfigurationMutationKind>(entity.MutationKind),
            Granularity = Enum.Parse<ConfigurationOverrideGranularity>(entity.Granularity),
            State = Enum.Parse<ConfigurationValueState>(entity.State),
            NewValue = JsonToStoredValue(entity.NewValueJson) ?? ConfigurationStoredValue.Null,
            OldValue = JsonToStoredValue(entity.OldValueJson),
            Version = entity.Version,
            SchemaVersion = entity.SchemaVersion,
            ModifiedTime = entity.ModifiedTime,
            ModifierId = entity.ModifierId,
            ModifierName = entity.ModifierName,
            Reason = entity.Reason
        };
    }

    private static ConfigurationStoredValue ToStoredValue(ConfigurationValueOverrideEntity entity)
    {
        return new ConfigurationStoredValue
        {
            Kind = Enum.Parse<ConfigurationStoredValueKind>(entity.StoredValueKind),
            PlainJson = entity.PlainJson,
            ProtectedPayload = entity.ProtectedPayload,
            SecretReference = entity.SecretReference
        };
    }

    private static string StoredValueToJson(ConfigurationStoredValue value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    private static ConfigurationStoredValue? JsonToStoredValue(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ConfigurationStoredValue>(json);
    }

    private static ConfigurationMutationResult CreateResult(ConfigurationSourceMutation mutation, long version, DateTimeOffset now)
    {
        return new ConfigurationMutationResult
        {
            DefinitionKey = mutation.Request.DefinitionKey,
            LogicalPath = mutation.Request.LogicalPath,
            NewVersion = version,
            SchemaVersion = EffectiveSchemaVersion(mutation),
            ModifiedTime = now
        };
    }

    private static string ProjectConfigurationPath(string sectionPath, LogicalPath logicalPath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sectionPath))
        {
            parts.Add(sectionPath);
        }

        parts.AddRange(logicalPath.Segments.Select(segment => segment.Value));
        return string.Join(':', parts);
    }

    private static int EffectiveSchemaVersion(ConfigurationSourceMutation mutation)
    {
        return mutation.Request.ExpectedSchemaVersion > 0
            ? mutation.Request.ExpectedSchemaVersion
            : mutation.Definition.SchemaVersion;
    }
}
