using Monica.Configuration.Models;
using Monica.Core.Results;

namespace Monica.Configuration.Api;

internal sealed partial class ConfigurationApiService
{
    public async Task<ConfigurationMutationGroupPublishResult> PublishMutationGroupAsync(
        ConfigurationMutationGroupPublishRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Rejected(null, null, "Mutation group label is required.");
        }

        if (request.Changes.Count == 0)
        {
            return Rejected(null, null, "At least one configuration change is required.");
        }

        var beginResult = await facade.BeginMutationGroupAsync(
            request.Label,
            request.Reason,
            new ConfigurationMutationContext { Reason = request.Reason });
        if (beginResult.IsFailed(out var beginError, out var group))
        {
            return Rejected(null, null, beginError.Message ?? "Failed to begin configuration mutation group.");
        }

        var results = new List<ConfigurationMutationResult>();
        var definitionKeys = new List<string>();
        var latestVersionsByDefinition = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in request.Changes)
        {
            var result = change.TargetKind == ConfigurationMutationTargetKind.ExternalConfigurationSource
                ? await ApplyExternalChangeAsync(change, group.GroupId, request.Reason)
                : await ApplyMonicaChangeAsync(change, group.GroupId, request.Reason, latestVersionsByDefinition);

            if (result.IsFailed(out var mutationError, out var mutationResult))
            {
                if (results.Count > 0)
                {
                    await facade.MarkMutationGroupPartialAsync(
                        group.GroupId,
                        results.Count,
                        definitionKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
                    return new ConfigurationMutationGroupPublishResult
                    {
                        Status = ConfigurationMutationGroupPublishStatus.PartiallyApplied,
                        Group = group with
                        {
                            MutationCount = results.Count,
                            DefinitionKeys = definitionKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                            Status = ConfigurationMutationGroupStatus.PartiallyApplied
                        },
                        Results = results,
                        FailedChange = change,
                        FailureMessage = mutationError.Message
                    };
                }

                return Rejected(group, change, mutationError.Message ?? "Configuration mutation failed.");
            }

            results.Add(mutationResult);
            definitionKeys.Add(change.DefinitionKey);
            if (change.TargetKind == ConfigurationMutationTargetKind.MonicaEffectiveStore)
            {
                latestVersionsByDefinition[change.DefinitionKey] = mutationResult.NewVersion;
            }
        }

        var completeResult = await facade.CompleteMutationGroupAsync(
            group.GroupId,
            results.Count,
            definitionKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        if (completeResult.IsFailed(out var completeError))
        {
            return new ConfigurationMutationGroupPublishResult
            {
                Status = ConfigurationMutationGroupPublishStatus.PartiallyApplied,
                Group = group,
                Results = results,
                FailureMessage = completeError.Message
            };
        }

        return new ConfigurationMutationGroupPublishResult
        {
            Status = ConfigurationMutationGroupPublishStatus.Applied,
            Group = group with
            {
                MutationCount = results.Count,
                DefinitionKeys = definitionKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Status = ConfigurationMutationGroupStatus.Applied
            },
            Results = results
        };
    }

    public async Task<ConfigurationValueHistory?> GetHistoryByIdAsync(string historyId)
    {
        return await historyService.GetHistoryByIdAsync(historyId, CancellationToken.None);
    }

    private async Task<Res<ConfigurationMutationResult>> ApplyMonicaChangeAsync(
        ConfigurationMutationGroupPublishChange change,
        string groupId,
        string? reason,
        IReadOnlyDictionary<string, long?> latestVersionsByDefinition)
    {
        var expectedValueVersion = latestVersionsByDefinition.GetValueOrDefault(
            change.DefinitionKey,
            change.ExpectedValueVersion);

        return await facade.MutateAsync(new ConfigurationMutationRequest
        {
            DefinitionKey = change.DefinitionKey,
            LogicalPath = ParsePath(change.LogicalPath),
            MutationKind = change.MutationKind,
            Value = ToStoredValue(change),
            ExpectedSchemaVersion = change.ExpectedSchemaVersion,
            ExpectedValueVersion = expectedValueVersion,
            Context = new ConfigurationMutationContext
            {
                MutationGroupId = groupId,
                Reason = reason
            }
        });
    }

    private async Task<Res<ConfigurationMutationResult>> ApplyExternalChangeAsync(
        ConfigurationMutationGroupPublishChange change,
        string groupId,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(change.SourceKey))
        {
            return Res.Fail("External configuration source mutations require a source key.");
        }

        var expectedRevision = change.ExpectedSourceRevision;
        if (string.IsNullOrWhiteSpace(expectedRevision))
        {
            expectedRevision = await LoadSourceRevisionOrNullAsync(change.SourceKey);
        }

        return await facade.MutateSourceAsync(new ConfigurationSourceMutationRequest
        {
            SourceKey = change.SourceKey,
            DefinitionKey = change.DefinitionKey,
            LogicalPath = ParsePath(change.LogicalPath),
            MutationKind = change.MutationKind,
            Value = ToStoredValue(change),
            ExpectedSchemaVersion = change.ExpectedSchemaVersion,
            ExpectedSourceRevision = expectedRevision,
            Context = new ConfigurationMutationContext
            {
                MutationGroupId = groupId,
                Reason = reason
            }
        });
    }

}
