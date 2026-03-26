using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DevOps.Git.Events;
using Monica.EventBus.Abstractions.Handlers;
using Monica.Markdown.Interfaces;
using Monica.Modules;

namespace Monica.Markdown.Events;

/// <summary>
/// Refreshes Markdown document groups bound to Git repositories after Git events.
/// </summary>
public class MarkdownGitBindingRefreshEventHandler(
    IOptions<ModuleMarkdownOption> options,
    IMoMarkdownService markdownService,
    ILogger<MarkdownGitBindingRefreshEventHandler> logger)
    : IMoLocalEventHandler<GitRepositoryUpdatedEvent>, IMoLocalEventHandler<GitRepositoryDeletedEvent>
{
    Task IMoLocalEventHandler<GitRepositoryUpdatedEvent>.HandleEventAsync(GitRepositoryUpdatedEvent eventData)
    {
        return RefreshBoundGroupsAsync(eventData.RepositoryId, $"sync ({eventData.Trigger})");
    }

    Task IMoLocalEventHandler<GitRepositoryDeletedEvent>.HandleEventAsync(GitRepositoryDeletedEvent eventData)
    {
        return RefreshBoundGroupsAsync(eventData.RepositoryId, "delete");
    }

    private async Task RefreshBoundGroupsAsync(string repositoryId, string operation)
    {
        var groupKeys = options.Value.GetBoundDocumentGroupKeys(repositoryId);
        if (groupKeys.Count == 0)
        {
            return;
        }

        var failures = new List<string>();
        foreach (var groupKey in groupKeys)
        {
            try
            {
                await markdownService.RefreshAsync(groupKey);
            }
            catch (Exception ex)
            {
                failures.Add($"{groupKey}: {ex.Message}");
                logger.LogWarning(
                    ex,
                    "Failed to refresh Markdown document group '{GroupKey}' for Git repository '{RepositoryId}' after {Operation}.",
                    groupKey,
                    repositoryId,
                    operation);
            }
        }

        if (failures.Count == 0)
        {
            logger.LogInformation(
                "Refreshed {Count} Markdown document group(s) for Git repository '{RepositoryId}' after {Operation}.",
                groupKeys.Count,
                repositoryId,
                operation);
            return;
        }

        logger.LogWarning(
            "Completed Markdown refresh for Git repository '{RepositoryId}' after {Operation} with warnings: {Warnings}",
            repositoryId,
            operation,
            string.Join("; ", failures));
    }
}
