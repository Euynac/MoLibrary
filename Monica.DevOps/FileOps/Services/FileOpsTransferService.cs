using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Monica.DevOps.FileOps.Abstractions;
using Monica.DevOps.FileOps.Exceptions;
using Monica.DevOps.FileOps.Models;
using Monica.DevOps.FileOps.Services.Support;
using Monica.Tool.Text;

namespace Monica.DevOps.FileOps.Services;

public class FileOpsTransferService(
    IFileOpsRuntimeConfigStore runtimeConfigStore,
    FileOpsPathPolicy pathPolicy,
    ILogger<FileOpsTransferService> logger)
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public async Task<FileOpsEntrySummary> SaveUploadAsync(
        string directoryPath,
        string fileName,
        long contentLength,
        Stream content,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        if (contentLength > runtimeConfig.MaxUploadFileSizeBytes)
        {
            throw FileOpsOperationException.UploadTooLarge(fileName, runtimeConfig.MaxUploadFileSizeBytes.FormatByteSize());
        }

        var targetPath = pathPolicy.ResolveChildPath(runtimeConfig, directoryPath, fileName);
        if ((File.Exists(targetPath) || Directory.Exists(targetPath)) && !overwrite)
        {
            throw FileOpsOperationException.EntryAlreadyExists(targetPath);
        }

        var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;
        await using var targetStream = new FileStream(targetPath, fileMode, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(targetStream, cancellationToken);

        var fileInfo = new FileInfo(targetPath);
        logger.LogInformation("Uploaded FileOps file to {Path}", targetPath);
        return new FileOpsEntrySummary
        {
            Name = fileInfo.Name,
            FullPath = fileInfo.FullName,
            Kind = FileOpsEntryKind.File,
            Extension = fileInfo.Extension,
            SizeBytes = fileInfo.Length,
            LastModifiedAt = fileInfo.LastWriteTimeUtc,
            IsReadOnly = pathPolicy.IsReadOnlyPath(runtimeConfig, fileInfo.FullName),
            CanDownload = fileInfo.Length <= runtimeConfig.MaxDownloadFileSizeBytes,
            CanEditText = fileInfo.Length <= runtimeConfig.MaxTextFileSizeBytes &&
                          runtimeConfig.CanEditExtension(fileInfo.Extension) &&
                          runtimeConfig.AllowWriteOperations &&
                          !pathPolicy.IsReadOnlyPath(runtimeConfig, fileInfo.FullName)
        };
    }

    public Task<FileOpsDownloadDescriptor> OpenDownloadAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var file = pathPolicy.ResolveFile(runtimeConfig, path);
        var fileInfo = new FileInfo(file.FullPath);
        if (fileInfo.Length > runtimeConfig.MaxDownloadFileSizeBytes)
        {
            throw FileOpsOperationException.DownloadTooLarge(file.FullPath, runtimeConfig.MaxDownloadFileSizeBytes.FormatByteSize());
        }

        if (!ContentTypeProvider.TryGetContentType(fileInfo.Name, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var stream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Task.FromResult(new FileOpsDownloadDescriptor
        {
            FullPath = file.FullPath,
            FileName = fileInfo.Name,
            ContentType = contentType,
            SizeBytes = fileInfo.Length,
            LastModifiedAt = fileInfo.LastWriteTimeUtc,
            ContentStream = stream
        });
    }
}
