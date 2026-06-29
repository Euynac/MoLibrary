using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.DevOps.FileOps.Exceptions;
using Monica.DevOps.Localization;
using Monica.DevOps.FileOps.Models;
using Monica.DevOps.FileOps.Services;
using Monica.DevOps.FileOps.Services.Support;
using Monica.Core.Results;

namespace Monica.DevOps.FileOps.Facades;

public class FileOpsFacade(
    FileOpsWorkspaceService workspaceService,
    FileOpsTransferService transferService,
    FileOpsMessageLocalizer messageLocalizer,
    IStringLocalizer<FileOpsResource> localizer,
    ILogger<FileOpsFacade> logger)
{
    public Task<Res<FileOpsRuntimeConfig>> GetRuntimeConfigAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            () => workspaceService.GetRuntimeConfigAsync(cancellationToken),
            ex => localizer["ServiceMessages:LoadRuntimeConfigFailed", messageLocalizer.TranslateExceptionMessage(ex)].Value,
            "Failed to load FileOps runtime configuration.");
    }

    public Task<Res> SaveRuntimeConfigAsync(FileOpsRuntimeConfig runtimeConfig, CancellationToken cancellationToken = default)
    {
        var normalizedConfig = runtimeConfig.Clone().Normalize();
        return ExecuteAsync(
            () => workspaceService.UpdateRuntimeConfigAsync(normalizedConfig, cancellationToken),
            () => messageLocalizer.GetRuntimeConfigSavedMessage(normalizedConfig),
            ex => localizer["ServiceMessages:SaveRuntimeConfigFailed", messageLocalizer.TranslateExceptionMessage(ex)].Value,
            "Failed to save FileOps runtime configuration.");
    }

    public Task<Res<FileOpsBrowseResult>> BrowseAsync(string? path, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            () => workspaceService.BrowseAsync(path, cancellationToken),
            ex => localizer["ServiceMessages:BrowseFailed", path ?? string.Empty, messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to browse FileOps path {path ?? "(default)"}.");
    }

    public Task<Res<FileOpsFileTextContent>> ReadTextFileAsync(string path, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            () => workspaceService.ReadTextFileAsync(path, cancellationToken),
            ex => localizer["ServiceMessages:ReadTextFailed", path, messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to read FileOps text file {path}.");
    }

    public Task<Res> SaveTextFileAsync(FileOpsTextUpdateRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            () => workspaceService.SaveTextFileAsync(request, cancellationToken),
            () => messageLocalizer.GetTextFileSavedMessage(request.Path),
            ex => localizer["ServiceMessages:SaveTextFailed", request.Path, messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to save FileOps text file {request.Path}.");
    }

    public Task<Res> CreateDirectoryAsync(FileOpsCreateDirectoryRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async () =>
            {
                var createdPath = await workspaceService.CreateDirectoryAsync(request, cancellationToken);
                return messageLocalizer.GetDirectoryCreatedMessage(createdPath);
            },
            ex => localizer["ServiceMessages:CreateDirectoryFailed", request.ParentPath, messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to create FileOps directory in {request.ParentPath}.");
    }

    public Task<Res> DeleteEntryAsync(FileOpsDeleteRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async () =>
            {
                var result = await workspaceService.DeleteEntryAsync(request, cancellationToken);
                return messageLocalizer.GetEntryDeletedMessage(result);
            },
            ex => localizer["ServiceMessages:DeleteEntryFailed", request.Path, messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to delete FileOps entry {request.Path}.");
    }

    public Task<Res<FileOpsBatchDeleteResult>> DeleteEntriesAsync(
        FileOpsBatchDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            () => workspaceService.DeleteEntriesAsync(request, cancellationToken),
            ex => localizer["ServiceMessages:BatchDeleteFailed", messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to batch delete {request.Paths.Count} FileOps entries.");
    }

    public Task<Res<FileOpsUploadResult>> UploadAsync(
        string directoryPath,
        IReadOnlyCollection<FileOpsUploadPayload> files,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async () =>
            {
                if (files.Count == 0)
                {
                    throw FileOpsOperationException.UploadFilesRequired();
                }

                var uploadedEntries = new List<FileOpsEntrySummary>(files.Count);
                foreach (var file in files)
                {
                    uploadedEntries.Add(await transferService.SaveUploadAsync(
                        directoryPath,
                        file.FileName,
                        file.Length,
                        file.Content,
                        overwrite,
                        cancellationToken));
                }

                return new FileOpsUploadResult
                {
                    DirectoryPath = directoryPath,
                    Overwrite = overwrite,
                    UploadedEntries = uploadedEntries
                };
            },
            ex => localizer["ServiceMessages:UploadFailed", directoryPath, messageLocalizer.TranslateExceptionMessage(ex)].Value,
            $"Failed to upload FileOps files into {directoryPath}.");
    }

    private async Task<Res<T>> ExecuteAsync<T>(
        Func<Task<T>> action,
        Func<Exception, string> failureMessageFactory,
        string logMessage)
    {
        try
        {
            return Res.Ok(await action());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{LogMessage}", logMessage);
            return Res.Fail(failureMessageFactory(ex), GetResultStatus(ex));
        }
    }

    private async Task<Res> ExecuteAsync(
        Func<Task> action,
        Func<string> successMessageFactory,
        Func<Exception, string> failureMessageFactory,
        string logMessage)
    {
        try
        {
            await action();
            return Res.Ok(successMessageFactory());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{LogMessage}", logMessage);
            return Res.Fail(failureMessageFactory(ex), GetResultStatus(ex));
        }
    }

    private async Task<Res> ExecuteAsync(
        Func<Task<string>> action,
        Func<Exception, string> failureMessageFactory,
        string logMessage)
    {
        try
        {
            return Res.Ok(await action());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{LogMessage}", logMessage);
            return Res.Fail(failureMessageFactory(ex), GetResultStatus(ex));
        }
    }

    private static ResStatus GetResultStatus(Exception exception)
    {
        return exception switch
        {
            ArgumentException => ResStatus.BadRequest,
            InvalidOperationException => ResStatus.BadRequest,
            KeyNotFoundException => ResStatus.BadRequest,
            DirectoryNotFoundException => ResStatus.BadRequest,
            FileNotFoundException => ResStatus.BadRequest,
            IOException => ResStatus.BadRequest,
            UnauthorizedAccessException => ResStatus.Forbidden,
            FileOpsOperationException => ResStatus.BadRequest,
            _ => ResStatus.InternalError
        };
    }
}
