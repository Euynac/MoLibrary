using Microsoft.Extensions.Localization;
using Monica.DevOps.FileOps.Exceptions;
using Monica.DevOps.Localization;
using Monica.DevOps.FileOps.Models;

namespace Monica.DevOps.FileOps.Services.Support;

public class FileOpsMessageLocalizer(IStringLocalizer<FileOpsResource> localizer)
{
    public string GetRuntimeConfigSavedMessage(FileOpsRuntimeConfig runtimeConfig)
    {
        return localizer["Messages:RuntimeConfigSaved", runtimeConfig.AllowedRoots.Count].Value;
    }

    public string GetTextFileSavedMessage(string path)
    {
        return localizer["Messages:TextFileSaved", Path.GetFileName(path)].Value;
    }

    public string GetDirectoryCreatedMessage(string path)
    {
        return localizer["Messages:DirectoryCreated", Path.GetFileName(path)].Value;
    }

    public string GetEntryDeletedMessage(FileOpsDeleteResult result)
    {
        return localizer["Messages:EntryDeleted", LocalizeEntryKind(result.EntryKind), Path.GetFileName(result.Path)].Value;
    }

    public string GetFilesUploadedMessage(FileOpsUploadResult result)
    {
        return localizer["Messages:FilesUploaded", result.UploadedEntries.Count, result.DirectoryPath].Value;
    }

    public string TranslateExceptionMessage(Exception exception)
    {
        return exception is FileOpsOperationException operationException
            ? LocalizeGeneratedMessage(operationException.MessageCode, operationException.MessageArguments)
            : exception.Message;
    }

    public string LocalizeGeneratedMessage(FileOpsMessageCode messageCode, IReadOnlyList<string> arguments)
    {
        return messageCode switch
        {
            FileOpsMessageCode.PathRequired => localizer["GeneratedMessages:PathRequired"].Value,
            FileOpsMessageCode.AllowedRootsRequired => localizer["GeneratedMessages:AllowedRootsRequired"].Value,
            FileOpsMessageCode.ConfiguredRootMissing => localizer["GeneratedMessages:ConfiguredRootMissing", arguments[0]].Value,
            FileOpsMessageCode.PathOutsideAllowedRoots => localizer["GeneratedMessages:PathOutsideAllowedRoots", arguments[0]].Value,
            FileOpsMessageCode.EntryNotFound => localizer["GeneratedMessages:EntryNotFound", arguments[0]].Value,
            FileOpsMessageCode.DirectoryExpected => localizer["GeneratedMessages:DirectoryExpected", arguments[0]].Value,
            FileOpsMessageCode.FileExpected => localizer["GeneratedMessages:FileExpected", arguments[0]].Value,
            FileOpsMessageCode.HiddenEntriesDisabled => localizer["GeneratedMessages:HiddenEntriesDisabled", arguments[0]].Value,
            FileOpsMessageCode.WriteOperationsDisabled => localizer["GeneratedMessages:WriteOperationsDisabled"].Value,
            FileOpsMessageCode.DeleteOperationsDisabled => localizer["GeneratedMessages:DeleteOperationsDisabled"].Value,
            FileOpsMessageCode.PathReadOnly => localizer["GeneratedMessages:PathReadOnly", arguments[0]].Value,
            FileOpsMessageCode.InvalidEntryName => localizer["GeneratedMessages:InvalidEntryName", arguments[0]].Value,
            FileOpsMessageCode.EntryAlreadyExists => localizer["GeneratedMessages:EntryAlreadyExists", arguments[0]].Value,
            FileOpsMessageCode.TextFileTooLarge => localizer["GeneratedMessages:TextFileTooLarge", arguments[0], arguments[1]].Value,
            FileOpsMessageCode.BinaryFilePreviewUnsupported => localizer["GeneratedMessages:BinaryFilePreviewUnsupported", arguments[0]].Value,
            FileOpsMessageCode.UploadTooLarge => localizer["GeneratedMessages:UploadTooLarge", arguments[0], arguments[1]].Value,
            FileOpsMessageCode.DownloadTooLarge => localizer["GeneratedMessages:DownloadTooLarge", arguments[0], arguments[1]].Value,
            FileOpsMessageCode.CannotDeleteRoot => localizer["GeneratedMessages:CannotDeleteRoot", arguments[0]].Value,
            FileOpsMessageCode.UploadFilesRequired => localizer["GeneratedMessages:UploadFilesRequired"].Value,
            FileOpsMessageCode.EditableExtensionRequired => localizer["GeneratedMessages:EditableExtensionRequired", arguments[0]].Value,
            _ => throw new ArgumentOutOfRangeException(nameof(messageCode), messageCode, null)
        };
    }

    public string LocalizeEntryKind(FileOpsEntryKind entryKind)
    {
        return entryKind switch
        {
            FileOpsEntryKind.Directory => localizer["Labels:Directory"].Value,
            FileOpsEntryKind.File => localizer["Labels:File"].Value,
            _ => entryKind.ToString()
        };
    }
}
