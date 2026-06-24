using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Core.Extensions;
using Monica.Core.Results;

namespace Monica.Configuration.Api;

internal sealed class ConfigurationApiFacade(
    ConfigurationFacade configurationFacade,
    ConfigurationApiService apiService)
{
    /// <summary>
    /// Gets flattened configuration parameter rows for external frontends.
    /// </summary>
    public async Task<Res<ConfigurationParameterListResponse>> GetParametersAsync(ConfigurationParameterQuery query)
    {
        return await ToResAsync(() => apiService.GetParametersAsync(query), "Failed to get configuration parameters");
    }

    /// <summary>
    /// Gets all configuration definition summaries.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationDefinitionSummary>>> GetDefinitionsAsync()
    {
        return configurationFacade.GetDefinitionsAsync();
    }

    /// <summary>
    /// Gets one configuration definition.
    /// </summary>
    public Task<Res<ConfigurationDefinitionDetail>> GetDefinitionAsync(string definitionKey)
    {
        return configurationFacade.GetDefinitionAsync(definitionKey);
    }

    /// <summary>
    /// Gets schema publish history for a definition.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationDefinitionPublishHistory>>> GetDefinitionPublishHistoriesAsync(
        string definitionKey,
        int limit)
    {
        return configurationFacade.GetDefinitionPublishHistoriesAsync(definitionKey, limit);
    }

    /// <summary>
    /// Gets the display-safe effective value for one logical path.
    /// </summary>
    public async Task<Res<ConfigurationEffectiveValue>> GetEffectiveValueAsync(string definitionKey, string? logicalPath)
    {
        return await ToFacadeResAsync(
            () => configurationFacade.GetEffectiveValueAsync(definitionKey, LogicalPath.Parse(logicalPath ?? string.Empty)),
            "Failed to get configuration effective value");
    }

    /// <summary>
    /// Gets source chain details for one logical path.
    /// </summary>
    public async Task<Res<ConfigurationSourceChain>> GetSourceChainAsync(string definitionKey, string? logicalPath)
    {
        return await ToFacadeResAsync(
            () => configurationFacade.GetSourceChainAsync(definitionKey, LogicalPath.Parse(logicalPath ?? string.Empty)),
            "Failed to get configuration source chain");
    }

    /// <summary>
    /// Gets runtime configuration source descriptors.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationSourceDescriptor>>> GetConfigurationSourcesAsync()
    {
        return configurationFacade.GetConfigurationSourcesAsync();
    }

    /// <summary>
    /// Gets source inventories.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationSourceInventory>>> GetConfigurationSourceInventoriesAsync()
    {
        return configurationFacade.GetConfigurationSourceInventoriesAsync();
    }

    /// <summary>
    /// Gets an external source revision.
    /// </summary>
    public Task<Res<string?>> GetSourceRevisionAsync(string sourceKey)
    {
        return configurationFacade.GetSourceRevisionAsync(sourceKey);
    }

    /// <summary>
    /// Gets a display-safe source file view.
    /// </summary>
    public Task<Res<ConfigurationSourceFileView>> GetSourceFileViewAsync(string sourceKey)
    {
        return configurationFacade.GetSourceFileViewAsync(sourceKey);
    }

    /// <summary>
    /// Builds an editable JSON document for an external frontend.
    /// </summary>
    public async Task<Res<ConfigurationJsonEditorDocumentResponse>> BuildJsonEditorDocumentAsync(
        string definitionKey,
        string? scopePath)
    {
        return await ToResAsync(
            () => apiService.BuildJsonEditorDocumentAsync(definitionKey, scopePath),
            "Failed to build configuration JSON editor document");
    }

    /// <summary>
    /// Analyzes edited JSON into saveable draft changes.
    /// </summary>
    public async Task<Res<ConfigurationJsonDraftAnalyzeResult>> AnalyzeJsonDraftAsync(
        ConfigurationJsonDraftAnalyzeRequest request)
    {
        var result = await ToResAsync(
            () => apiService.AnalyzeJsonDraftAsync(request),
            "Failed to analyze configuration JSON draft");

        if (result.IsFailed(out _, out var data) || !data.HasBlockingIssues)
        {
            return result;
        }

        result.Status = ResStatus.ValidateError;
        result.Message = "Configuration JSON draft contains validation issues.";
        return result;
    }

    /// <summary>
    /// Publishes a mutation group.
    /// </summary>
    public async Task<Res<ConfigurationMutationGroupPublishResult>> PublishMutationGroupAsync(
        ConfigurationMutationGroupPublishRequest request)
    {
        var result = await ToResAsync(
            () => apiService.PublishMutationGroupAsync(request),
            "Failed to publish configuration mutation group");

        if (result.IsFailed(out _, out var data))
        {
            return result;
        }

        return data.Status switch
        {
            ConfigurationMutationGroupPublishStatus.Applied => result,
            ConfigurationMutationGroupPublishStatus.PartiallyApplied => WithStatus(
                result,
                ResStatus.ErrorWarning,
                data.FailureMessage ?? "Configuration mutation group was partially applied."),
            _ => WithStatus(
                result,
                ResStatus.BadRequest,
                data.FailureMessage ?? "Configuration mutation group was rejected.")
        };
    }

    /// <summary>
    /// Lists mutation groups.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationMutationGroup>>> GetMutationGroupsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey)
    {
        return configurationFacade.GetMutationGroupsAsync(from, to, definitionKey);
    }

    /// <summary>
    /// Gets one mutation group.
    /// </summary>
    public Task<Res<ConfigurationMutationGroup>> GetMutationGroupAsync(string groupId)
    {
        return configurationFacade.GetMutationGroupAsync(groupId);
    }

    /// <summary>
    /// Gets history rows for one mutation group.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationValueHistory>>> GetGroupHistoryAsync(string groupId)
    {
        return configurationFacade.GetGroupHistoryAsync(groupId);
    }

    /// <summary>
    /// Queries mutation history.
    /// </summary>
    public async Task<Res<IReadOnlyList<ConfigurationValueHistory>>> QueryHistoryAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? definitionKey,
        string? logicalPath,
        string? mutationGroupId)
    {
        return await ToFacadeResAsync(
            () => configurationFacade.QueryHistoryAsync(
                from,
                to,
                definitionKey,
                string.IsNullOrWhiteSpace(logicalPath) ? null : LogicalPath.Parse(logicalPath),
                mutationGroupId),
            "Failed to query configuration history");
    }

    /// <summary>
    /// Gets one history row by identity.
    /// </summary>
    public async Task<Res<ConfigurationValueHistory>> GetHistoryByIdAsync(string historyId)
    {
        var result = await ToResAsync(
            () => apiService.GetHistoryByIdAsync(historyId),
            "Failed to get configuration history");

        if (result.IsFailed(out var error, out var history))
        {
            return error;
        }

        return history is null
            ? Res.Fail($"Configuration history '{historyId}' was not found.")
            : history;
    }

    /// <summary>
    /// Rolls one history row back.
    /// </summary>
    public Task<Res<ConfigurationMutationResult>> RollbackHistoryAsync(string historyId, ConfigurationRollbackRequest? request)
    {
        return configurationFacade.RollbackHistoryAsync(historyId, request?.Reason);
    }

    /// <summary>
    /// Rolls selected history rows back.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationMutationResult>>> RollbackHistoriesAsync(
        ConfigurationRollbackHistoriesRequest request)
    {
        return configurationFacade.RollbackHistoriesAsync(request.HistoryIds, request.Reason);
    }

    /// <summary>
    /// Rolls one mutation group back.
    /// </summary>
    public Task<Res<IReadOnlyList<ConfigurationMutationResult>>> RollbackGroupAsync(
        string groupId,
        ConfigurationRollbackRequest? request)
    {
        return configurationFacade.RollbackGroupAsync(groupId, request?.Reason);
    }

    /// <summary>
    /// Creates a configuration export package.
    /// </summary>
    public async Task<Res<ConfigurationExportDocument>> CreateExportAsync(ConfigurationApiExportRequest request)
    {
        return await ToResAsync(() => apiService.CreateExportAsync(request), "Failed to export configuration package");
    }

    /// <summary>
    /// Analyzes a configuration import package.
    /// </summary>
    public async Task<Res<ConfigurationImportAnalyzeResult>> AnalyzeImportAsync(ConfigurationImportAnalyzeRequest request)
    {
        var result = await ToResAsync(() => apiService.AnalyzeImportAsync(request), "Failed to analyze configuration import package");
        if (result.IsFailed(out _, out var data) || !data.HasBlockingIssues)
        {
            return result;
        }

        return WithStatus(result, ResStatus.ValidateError, "Configuration import contains validation issues.");
    }

    /// <summary>
    /// Analyzes and publishes a configuration import package.
    /// </summary>
    public async Task<Res<ConfigurationImportPublishResult>> PublishImportAsync(ConfigurationImportPublishRequest request)
    {
        var result = await ToResAsync(() => apiService.PublishImportAsync(request), "Failed to publish configuration import package");
        if (result.IsFailed(out _, out var data))
        {
            return result;
        }

        if (data.PublishResult is null)
        {
            return data.Report.HasBlockingIssues
                ? WithStatus(result, ResStatus.ValidateError, "Configuration import contains validation issues.")
                : WithStatus(result, ResStatus.ErrorWarning, "Configuration import contains warnings and requires confirmation.");
        }

        return data.PublishResult.Status switch
        {
            ConfigurationMutationGroupPublishStatus.Applied => result,
            ConfigurationMutationGroupPublishStatus.PartiallyApplied => WithStatus(
                result,
                ResStatus.ErrorWarning,
                data.PublishResult.FailureMessage ?? "Configuration import was partially applied."),
            _ => WithStatus(
                result,
                ResStatus.BadRequest,
                data.PublishResult.FailureMessage ?? "Configuration import was rejected.")
        };
    }

    private static async Task<Res<T>> ToResAsync<T>(Func<Task<T>> action, string failurePrefix)
    {
        try
        {
            return Res.Ok(await action());
        }
        catch (Exception ex)
        {
            return Res.Fail($"{failurePrefix}: {ex.GetMessageRecursively()}");
        }
    }

    private static async Task<Res<T>> ToFacadeResAsync<T>(Func<Task<Res<T>>> action, string failurePrefix)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return Res.Fail($"{failurePrefix}: {ex.GetMessageRecursively()}");
        }
    }

    private static Res<T> WithStatus<T>(Res<T> result, ResStatus status, string message)
    {
        result.Status = status;
        result.Message = message;
        return result;
    }
}
