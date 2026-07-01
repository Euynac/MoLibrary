using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

internal sealed class ConfigurationUnifiedVersionCoordinator(
    IConfigurationUnifiedVersionStore versionStore,
    IConfigurationHistoryService historyService,
    ConfigurationUnifiedVersionSnapshotFactory snapshotFactory)
    : IConfigurationUnifiedVersionCoordinator
{
    public async Task CaptureStandaloneMutationAsync(ConfigurationValueHistory history, CancellationToken cancellationToken)
    {
        var request = await snapshotFactory.CreateRequestAsync(
            [history.DefinitionKey],
            null,
            new ConfigurationMutationContext
            {
                ModifierId = history.ModifierId,
                ModifierName = history.ModifierName,
                Reason = history.Reason
            },
            history.ModifiedTime,
            cancellationToken);
        if (request is null)
        {
            return;
        }

        await versionStore.AppendVersionAsync(request, cancellationToken);
    }

    public async Task CaptureMutationGroupAsync(ConfigurationMutationGroup group, CancellationToken cancellationToken)
    {
        if (!snapshotFactory.IsEnabled || !string.IsNullOrWhiteSpace(group.RolledBackGroupId))
        {
            return;
        }

        if (await versionStore.GetVersionByMutationGroupAsync(group.GroupId, cancellationToken) is not null)
        {
            return;
        }

        var histories = await historyService.QueryHistoryAsync(
            null,
            null,
            null,
            null,
            group.GroupId,
            cancellationToken);
        var triggerDefinitionKeys = histories
            .Select(static history => history.DefinitionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var request = await snapshotFactory.CreateRequestAsync(
            triggerDefinitionKeys,
            group.GroupId,
            new ConfigurationMutationContext
            {
                ModifierId = group.ModifierId,
                ModifierName = group.ModifierName,
                Reason = group.Reason
            },
            group.CreatedTime,
            cancellationToken);
        if (request is null)
        {
            return;
        }

        await versionStore.AppendVersionAsync(request, cancellationToken);
    }
}
