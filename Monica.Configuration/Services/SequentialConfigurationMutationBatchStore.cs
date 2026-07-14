using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Core.Extensions;

namespace Monica.Configuration.Services;

/// <summary>
/// Preserves mutation ordering and reports partial outcomes for stores without transactional batches.
/// </summary>
internal sealed class SequentialConfigurationMutationBatchStore(
    IConfigurationEffectiveValueStore effectiveValueStore,
    IConfigurationHistoryStore historyStore,
    ILogger<SequentialConfigurationMutationBatchStore> logger)
    : IConfigurationMutationBatchStore
{
    public async Task<ConfigurationMutationBatchCommitResult> CommitAsync(
        ConfigurationMutationBatchCommitRequest request,
        CancellationToken cancellationToken)
    {
        var documents = new Dictionary<string, ConfigurationEffectiveValueDocument>(StringComparer.OrdinalIgnoreCase);
        var appliedRequestIds = new List<string>(request.Items.Count);
        var issues = new List<ConfigurationPostCommitIssue>();
        ConfigurationMutationBatchFailure? failure = null;

        foreach (var item in request.Items)
        {
            ConfigurationEffectiveValueDocument document;
            try
            {
                document = await effectiveValueStore.SaveAsync(item.SaveRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                failure = new ConfigurationMutationBatchFailure
                {
                    RequestId = item.RequestId,
                    Message = ex.GetMessageRecursively(),
                    Detail = ex.ToString()
                };
                break;
            }

            appliedRequestIds.Add(item.RequestId);
            documents[document.DefinitionKey] = document;
            try
            {
                await historyStore.AppendHistoryAsync(item.History, cancellationToken);
            }
            catch (Exception ex)
            {
                issues.Add(CreateIssue(
                    ConfigurationPostCommitIssueKind.AuditFinalization,
                    historyStore.GetType().Name,
                    "A configuration value was saved, but its history row could not be persisted.",
                    ex));
            }
        }

        var persistedGroup = request.MutationGroup with
        {
            MutationCount = appliedRequestIds.Count,
            DefinitionKeys = documents.Keys.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToArray(),
            Status = failure is null
                ? request.MutationGroup.Status
                : ConfigurationMutationGroupStatus.PartiallyApplied
        };
        try
        {
            await historyStore.UpsertGroupAsync(persistedGroup, cancellationToken);
        }
        catch (Exception ex)
        {
            issues.Add(CreateIssue(
                ConfigurationPostCommitIssueKind.AuditFinalization,
                historyStore.GetType().Name,
                "Configuration values were saved, but the mutation-group audit row could not be persisted.",
                ex));
        }

        return new ConfigurationMutationBatchCommitResult
        {
            MutationGroup = persistedGroup,
            Documents = documents,
            AppliedRequestIds = appliedRequestIds,
            Failure = failure,
            PostCommitIssues = issues
        };
    }

    private ConfigurationPostCommitIssue CreateIssue(
        ConfigurationPostCommitIssueKind kind,
        string source,
        string message,
        Exception exception)
    {
        logger.LogWarning(exception, "{Message} Source: {Source}", message, source);
        return new ConfigurationPostCommitIssue
        {
            Kind = kind,
            Source = source,
            Message = message,
            Detail = exception.ToString()
        };
    }
}
