using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

internal sealed partial class ConfigurationMutationGroupApplyService
{
    private async Task<MonicaCommitPlan> BuildMonicaCommitPlanAsync(
        ConfigurationMutationGroupApplyRequest request,
        ConfigurationMutationContext context,
        DateTimeOffset createdTime,
        IReadOnlyList<PreparedConfigurationMutation> mutations,
        bool hasExternalMutations,
        CancellationToken cancellationToken)
    {
        var states = new Dictionary<string, DefinitionMutationState>(StringComparer.OrdinalIgnoreCase);
        var commitItems = new List<ConfigurationMutationBatchCommitItem>(mutations.Count);
        var plannedResults = new List<PlannedMutationResult>(mutations.Count);

        foreach (var mutation in mutations)
        {
            var target = (ConfigurationEffectiveStoreMutationTarget)mutation.Command.Target;
            if (!states.TryGetValue(mutation.Definition.DefinitionKey, out var state))
            {
                var document = await effectiveValueStore.GetAsync(
                    mutation.Definition.DefinitionKey,
                    cancellationToken)
                    ?? new ConfigurationEffectiveValueDocument
                    {
                        DefinitionKey = mutation.Definition.DefinitionKey,
                        Json = seedFactory.CreateSeedJson(mutation.Definition),
                        Version = 0,
                        SchemaVersion = mutation.Definition.SchemaVersion,
                        LastModifiedTime = createdTime
                    };
                if (target.ExpectedVersion is not null && target.ExpectedVersion != document.Version)
                {
                    throw new ConfigurationConcurrencyConflictException(
                        $"Expected version {target.ExpectedVersion} for '{mutation.Definition.DefinitionKey}', but current version is {document.Version}.");
                }

                state = new DefinitionMutationState(document, target.ExpectedVersion);
                states.Add(mutation.Definition.DefinitionKey, state);
            }
            else if (state.StagedExpectedVersion != target.ExpectedVersion)
            {
                throw new ConfigurationValidationFailedException(
                    $"Mutation group contains inconsistent expected versions for '{mutation.Definition.DefinitionKey}'.");
            }

            var oldValue = documentEditor.ReadValue(
                mutation.Definition,
                state.Json,
                mutation.Request.LogicalPath);
            var updatedJson = documentEditor.ApplyMutation(mutation.Definition, state.Json, mutation.Request);
            var newValue = mutation.Request.MutationKind == ConfigurationMutationKind.Remove
                ? ConfigurationStoredValue.Null
                : documentEditor.ReadValue(mutation.Definition, updatedJson, mutation.Request.LogicalPath)
                  ?? ConfigurationStoredValue.Null;
            var modifiedTime = DateTimeOffset.UtcNow;
            var newVersion = state.Version + 1;
            var history = new ConfigurationValueHistory
            {
                HistoryId = Guid.NewGuid().ToString("N"),
                DefinitionKey = mutation.Definition.DefinitionKey,
                LogicalPath = mutation.Request.LogicalPath,
                ConfigurationPath = mutation.ConfigurationPath,
                MutationKind = mutation.Request.MutationKind,
                Granularity = mutation.Granularity,
                State = mutation.Request.MutationKind == ConfigurationMutationKind.Remove
                    ? ConfigurationValueState.Removed
                    : ConfigurationValueState.Active,
                OldValue = oldValue,
                NewValue = newValue,
                Version = newVersion,
                SchemaVersion = mutation.Definition.SchemaVersion,
                ModifiedTime = modifiedTime,
                ModifierId = context.ModifierId,
                ModifierName = context.ModifierName,
                Reason = context.Reason,
                MutationGroupId = context.MutationGroupId
            };
            commitItems.Add(new ConfigurationMutationBatchCommitItem
            {
                RequestId = mutation.Command.RequestId,
                SaveRequest = new ConfigurationEffectiveValueSaveRequest
                {
                    Definition = mutation.Definition,
                    Json = updatedJson,
                    ExpectedVersion = state.Version,
                    Context = context
                },
                History = history
            });
            var result = new ConfigurationMutationResult
            {
                DefinitionKey = mutation.Definition.DefinitionKey,
                LogicalPath = mutation.Request.LogicalPath,
                NewVersion = newVersion,
                SchemaVersion = mutation.Definition.SchemaVersion,
                ModifiedTime = modifiedTime,
                RequiresRestart = mutation.TargetNode
                    .ResolveEffectiveReloadBehavior(mutation.Definition)
                    .RequiresProcessRestart()
            };
            plannedResults.Add(new PlannedMutationResult(mutation.Command.RequestId, result));
            state.Advance(updatedJson, newVersion, mutation.Definition.SchemaVersion, modifiedTime, context);
        }

        var definitionKeys = mutations
            .Select(static mutation => mutation.Definition.DefinitionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var group = new ConfigurationMutationGroup
        {
            GroupId = context.MutationGroupId!,
            Label = NormalizeLabel(request.Label, createdTime),
            Reason = context.Reason,
            DefinitionKeys = definitionKeys,
            MutationCount = mutations.Count,
            CreatedTime = createdTime,
            ModifierId = context.ModifierId,
            ModifierName = context.ModifierName,
            Status = hasExternalMutations
                ? ConfigurationMutationGroupStatus.PartiallyApplied
                : ConfigurationMutationGroupStatus.Applied
        };
        ConfigurationUnifiedVersionCreateRequest? unifiedVersion = null;
        if (!hasExternalMutations)
        {
            unifiedVersion = await unifiedVersionSnapshotFactory.CreateRequestAsync(
                definitionKeys,
                group.GroupId,
                context,
                createdTime,
                cancellationToken,
                states.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToDocument(),
                    StringComparer.OrdinalIgnoreCase));
        }

        return new MonicaCommitPlan(
            new ConfigurationMutationBatchCommitRequest
            {
                MutationGroup = group,
                Items = commitItems,
                UnifiedVersion = unifiedVersion
            },
            plannedResults);
    }

    private sealed class DefinitionMutationState(
        ConfigurationEffectiveValueDocument document,
        long? stagedExpectedVersion)
    {
        public string DefinitionKey { get; } = document.DefinitionKey;

        public string Json { get; private set; } = document.Json;

        public long Version { get; private set; } = document.Version;

        public int SchemaVersion { get; private set; } = document.SchemaVersion;

        public DateTimeOffset ModifiedTime { get; private set; } = document.LastModifiedTime;

        public string? ModifierId { get; private set; } = document.LastModifierId;

        public string? ModifierName { get; private set; } = document.LastModifierName;

        public long? StagedExpectedVersion { get; } = stagedExpectedVersion;

        public void Advance(
            string json,
            long version,
            int schemaVersion,
            DateTimeOffset modifiedTime,
            ConfigurationMutationContext context)
        {
            Json = json;
            Version = version;
            SchemaVersion = schemaVersion;
            ModifiedTime = modifiedTime;
            ModifierId = context.ModifierId;
            ModifierName = context.ModifierName;
        }

        public ConfigurationEffectiveValueDocument ToDocument()
        {
            return new ConfigurationEffectiveValueDocument
            {
                DefinitionKey = DefinitionKey,
                Json = Json,
                Version = Version,
                SchemaVersion = SchemaVersion,
                LastModifiedTime = ModifiedTime,
                LastModifierId = ModifierId,
                LastModifierName = ModifierName
            };
        }
    }

    private sealed record PlannedMutationResult(string RequestId, ConfigurationMutationResult Result);

    private sealed record MonicaCommitPlan(
        ConfigurationMutationBatchCommitRequest CommitRequest,
        IReadOnlyList<PlannedMutationResult> PlannedResults);
}
