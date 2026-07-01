using System.Text.Json;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

internal sealed class ConfigurationUnifiedVersionService(
    IConfigurationUnifiedVersionStore versionStore,
    ConfigurationDefinitionResolver definitionResolver,
    IConfigurationEffectiveValueStore effectiveValueStore,
    IConfigurationMutationService mutationService,
    IConfigurationSourceMutationService sourceMutationService,
    IConfigurationMutationGroupService mutationGroupService,
    IConfigurationSourceInspector sourceInspector,
    IConfigurationJsonFileSourceWriter sourceWriter)
    : IConfigurationUnifiedVersionService
{
    public Task<IReadOnlyList<ConfigurationUnifiedVersionSummary>> ListVersionsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        int limit,
        CancellationToken cancellationToken)
    {
        return versionStore.ListVersionsAsync(from, to, definitionKey, limit, cancellationToken);
    }

    public Task<ConfigurationUnifiedVersionSnapshot?> GetVersionAsync(long version, CancellationToken cancellationToken)
    {
        return versionStore.GetVersionAsync(version, cancellationToken);
    }

    public async Task<ConfigurationUnifiedVersionComparison> CompareVersionsAsync(
        long originVersion,
        long targetVersion,
        CancellationToken cancellationToken)
    {
        var origin = await GetRequiredVersionAsync(originVersion, cancellationToken);
        var target = await GetRequiredVersionAsync(targetVersion, cancellationToken);
        var originByKey = origin.Definitions.ToDictionary(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        var targetByKey = target.Definitions.ToDictionary(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        var keys = originByKey.Keys.Concat(targetByKey.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase);
        var changes = new List<ConfigurationUnifiedVersionDefinitionChange>();

        foreach (var key in keys)
        {
            originByKey.TryGetValue(key, out var originDefinition);
            targetByKey.TryGetValue(key, out var targetDefinition);
            if (originDefinition is not null && targetDefinition is not null
                && JsonEquals(originDefinition.Json, targetDefinition.Json))
            {
                continue;
            }

            changes.Add(new ConfigurationUnifiedVersionDefinitionChange
            {
                DefinitionKey = key,
                DisplayName = targetDefinition?.DisplayName ?? originDefinition?.DisplayName ?? key,
                OriginJson = originDefinition?.Json,
                TargetJson = targetDefinition?.Json,
                ChangeKind = originDefinition is null
                    ? ConfigurationUnifiedVersionDefinitionChangeKind.Added
                    : targetDefinition is null
                        ? ConfigurationUnifiedVersionDefinitionChangeKind.Removed
                        : ConfigurationUnifiedVersionDefinitionChangeKind.Modified
            });
        }

        return new ConfigurationUnifiedVersionComparison
        {
            Origin = origin,
            Target = target,
            Changes = changes
        };
    }

    public async Task<ConfigurationUnifiedVersionApplyPreview> PreviewRollbackAsync(
        long version,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetRequiredVersionAsync(version, cancellationToken);
        var targets = new List<ConfigurationUnifiedVersionApplyTarget>();
        foreach (var document in snapshot.Definitions)
        {
            targets.Add(await ResolveApplyTargetAsync(document, cancellationToken));
        }

        return new ConfigurationUnifiedVersionApplyPreview
        {
            Version = version,
            Targets = targets
        };
    }

    public async Task<ConfigurationUnifiedVersionRollbackResult> RollbackToVersionAsync(
        long version,
        string? reason,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetRequiredVersionAsync(version, cancellationToken);
        var preview = await PreviewRollbackAsync(version, cancellationToken);
        var blocked = preview.Targets.FirstOrDefault(static target => target.Status != ConfigurationUnifiedVersionApplyTargetStatus.Ready);
        if (blocked is not null)
        {
            throw new InvalidOperationException(blocked.Diagnostic ?? $"Definition '{blocked.DefinitionKey}' cannot be restored.");
        }

        var group = await mutationGroupService.BeginAsync(
            $"Apply configuration version v{version}",
            reason,
            new ConfigurationMutationContext { Reason = reason },
            cancellationToken);
        var results = new List<ConfigurationMutationResult>();
        var definitionKeys = new List<string>();
        var context = new ConfigurationMutationContext
        {
            MutationGroupId = group.GroupId,
            Reason = reason
        };

        try
        {
            foreach (var document in snapshot.Definitions)
            {
                var target = preview.Targets.First(candidate =>
                    string.Equals(candidate.DefinitionKey, document.DefinitionKey, StringComparison.OrdinalIgnoreCase));
                results.Add(await ApplyDefinitionAsync(document, target, context, cancellationToken));
                definitionKeys.Add(document.DefinitionKey);
            }

            await mutationGroupService.CompleteAsync(group.GroupId, results.Count, definitionKeys, cancellationToken);
            var completedGroup = await mutationGroupService.GetAsync(group.GroupId, cancellationToken) ?? group;
            return new ConfigurationUnifiedVersionRollbackResult
            {
                Version = version,
                MutationGroup = completedGroup,
                Results = results
            };
        }
        catch
        {
            await mutationGroupService.MarkPartialAsync(group.GroupId, results.Count, definitionKeys, cancellationToken);
            throw;
        }
    }

    private async Task<ConfigurationUnifiedVersionSnapshot> GetRequiredVersionAsync(
        long version,
        CancellationToken cancellationToken)
    {
        return await versionStore.GetVersionAsync(version, cancellationToken)
               ?? throw new KeyNotFoundException($"Unified configuration version '{version}' was not found.");
    }

    private async Task<ConfigurationUnifiedVersionApplyTarget> ResolveApplyTargetAsync(
        ConfigurationUnifiedVersionDefinitionSnapshot document,
        CancellationToken cancellationToken)
    {
        ConfigurationDefinition definition;
        try
        {
            definition = await definitionResolver.GetRequiredAsync(document.DefinitionKey, cancellationToken);
        }
        catch (Exception ex)
        {
            return Blocked(
                document,
                ConfigurationUnifiedVersionApplyTargetStatus.MissingDefinition,
                $"Definition '{document.DefinitionKey}' is not known by the current process: {ex.Message}");
        }

        if (!string.Equals(definition.SchemaHash, document.SchemaHash, StringComparison.Ordinal))
        {
            return Blocked(
                document,
                ConfigurationUnifiedVersionApplyTargetStatus.SchemaMismatch,
                $"Definition '{document.DisplayName}' schema changed since version v{document.SchemaVersion}; rollback requires matching schema hash.");
        }

        var sources = sourceInspector.GetSources();
        var monicaSource = sources.FirstOrDefault(static source => source.Kind == ConfigurationSourceKind.MonicaEffectiveStore);
        var contributions = sourceInspector.GetDefinitionContributions(definition)
            .Where(static contribution => contribution.EffectiveValueCount > 0)
            .OrderByDescending(static contribution => contribution.Source.PriorityIndex)
            .ToArray();
        var writableWinner = contributions.FirstOrDefault(static contribution => contribution.Source.IsWritable);
        if (writableWinner is null)
        {
            var fallbackOverride = FindReadOnlyOverride(contributions, monicaSource);
            if (fallbackOverride is not null)
            {
                return Blocked(
                    document,
                    ConfigurationUnifiedVersionApplyTargetStatus.ReadOnlyOverride,
                    $"Read-only source '{fallbackOverride.Source.DisplayName}' has higher priority than Monica effective store.");
            }

            return MonicaTarget(document);
        }

        var readOnlyOverride = FindReadOnlyOverride(contributions, writableWinner.Source);
        if (readOnlyOverride is not null)
        {
            return Blocked(
                document,
                ConfigurationUnifiedVersionApplyTargetStatus.ReadOnlyOverride,
                $"Read-only source '{readOnlyOverride.Source.DisplayName}' has higher priority than writable source '{writableWinner.Source.DisplayName}'.");
        }

        return writableWinner.Source.Kind switch
        {
            ConfigurationSourceKind.MonicaEffectiveStore => MonicaTarget(document),
            ConfigurationSourceKind.JsonFile when string.IsNullOrWhiteSpace(definition.SectionPath) => Blocked(
                document,
                ConfigurationUnifiedVersionApplyTargetStatus.UnsupportedSource,
                $"Definition '{document.DisplayName}' uses the configuration root and cannot be written to a JSON source by unified version rollback."),
            ConfigurationSourceKind.JsonFile => new ConfigurationUnifiedVersionApplyTarget
            {
                DefinitionKey = document.DefinitionKey,
                DisplayName = document.DisplayName,
                SourceKey = writableWinner.Source.SourceKey,
                SourceDisplayName = writableWinner.Source.DisplayName,
                SourceKind = writableWinner.Source.Kind
            },
            _ => Blocked(
                document,
                ConfigurationUnifiedVersionApplyTargetStatus.UnsupportedSource,
                $"Source '{writableWinner.Source.DisplayName}' cannot be written by unified version rollback.")
        };
    }

    private static ConfigurationDefinitionSourceContribution? FindReadOnlyOverride(
        IEnumerable<ConfigurationDefinitionSourceContribution> contributions,
        ConfigurationSourceDescriptor? targetSource)
    {
        if (targetSource is null)
        {
            return contributions.FirstOrDefault(static contribution => !contribution.Source.IsWritable);
        }

        return contributions.FirstOrDefault(contribution =>
            contribution.Source.PriorityIndex > targetSource.PriorityIndex
            && !contribution.Source.IsWritable);
    }

    private async Task<ConfigurationMutationResult> ApplyDefinitionAsync(
        ConfigurationUnifiedVersionDefinitionSnapshot document,
        ConfigurationUnifiedVersionApplyTarget target,
        ConfigurationMutationContext context,
        CancellationToken cancellationToken)
    {
        var definition = await definitionResolver.GetRequiredAsync(document.DefinitionKey, cancellationToken);
        if (target.SourceKind == ConfigurationSourceKind.JsonFile && !string.IsNullOrWhiteSpace(target.SourceKey))
        {
            var source = sourceInspector.GetRequiredSource(target.SourceKey);
            var revision = await sourceWriter.GetRevisionAsync(source, cancellationToken);
            return await sourceMutationService.MutateAsync(new ConfigurationSourceMutationRequest
            {
                SourceKey = target.SourceKey,
                DefinitionKey = document.DefinitionKey,
                LogicalPath = LogicalPath.Root,
                MutationKind = ConfigurationMutationKind.Set,
                Value = ConfigurationStoredValue.FromJson(document.Json),
                ExpectedSchemaVersion = definition.SchemaVersion,
                ExpectedSourceRevision = revision,
                Context = context
            }, cancellationToken);
        }

        var currentDocument = await effectiveValueStore.GetAsync(document.DefinitionKey, cancellationToken);
        return await mutationService.MutateAsync(new ConfigurationMutationRequest
        {
            DefinitionKey = document.DefinitionKey,
            LogicalPath = LogicalPath.Root,
            MutationKind = ConfigurationMutationKind.Set,
            Value = ConfigurationStoredValue.FromJson(document.Json),
            ExpectedSchemaVersion = definition.SchemaVersion,
            ExpectedValueVersion = currentDocument?.Version,
            Context = context
        }, cancellationToken);
    }

    private static ConfigurationUnifiedVersionApplyTarget MonicaTarget(
        ConfigurationUnifiedVersionDefinitionSnapshot document)
    {
        return new ConfigurationUnifiedVersionApplyTarget
        {
            DefinitionKey = document.DefinitionKey,
            DisplayName = document.DisplayName,
            SourceKey = "monica:effective",
            SourceDisplayName = "Monica Effective Store",
            SourceKind = ConfigurationSourceKind.MonicaEffectiveStore
        };
    }

    private static ConfigurationUnifiedVersionApplyTarget Blocked(
        ConfigurationUnifiedVersionDefinitionSnapshot document,
        ConfigurationUnifiedVersionApplyTargetStatus status,
        string diagnostic)
    {
        return new ConfigurationUnifiedVersionApplyTarget
        {
            DefinitionKey = document.DefinitionKey,
            DisplayName = document.DisplayName,
            Status = status,
            Diagnostic = diagnostic
        };
    }

    private static bool JsonEquals(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return JsonSerializer.Serialize(leftDocument.RootElement) == JsonSerializer.Serialize(rightDocument.RootElement);
    }
}
