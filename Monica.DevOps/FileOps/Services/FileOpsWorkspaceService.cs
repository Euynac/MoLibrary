using Microsoft.Extensions.Logging;
using Monica.DevOps.FileOps.Abstractions;
using Monica.DevOps.FileOps.Exceptions;
using Monica.DevOps.FileOps.Models;
using Monica.DevOps.FileOps.Services.Support;
using Monica.Tool.General;

namespace Monica.DevOps.FileOps.Services;

public class FileOpsWorkspaceService(
    IFileOpsRuntimeConfigStore runtimeConfigStore,
    FileOpsPathPolicy pathPolicy,
    FileOpsTextInspector textInspector,
    ILogger<FileOpsWorkspaceService> logger)
{
    public Task<FileOpsRuntimeConfig> GetRuntimeConfigAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(runtimeConfigStore.GetCurrent());
    }

    public Task UpdateRuntimeConfigAsync(FileOpsRuntimeConfig runtimeConfig, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedConfig = runtimeConfig.Clone().Normalize();
        if (normalizedConfig.AllowedRoots.Count == 0)
        {
            throw FileOpsOperationException.AllowedRootsRequired();
        }

        foreach (var rootPath in normalizedConfig.AllowedRoots)
        {
            if (!Directory.Exists(rootPath))
            {
                throw FileOpsOperationException.ConfiguredRootMissing(rootPath);
            }
        }

        runtimeConfigStore.Update(normalizedConfig);
        logger.LogInformation(
            "Updated FileOps runtime configuration. Allowed roots: {AllowedRoots}. Read-only paths: {ReadOnlyPaths}. Write enabled: {AllowWrite}. Delete enabled: {AllowDelete}",
            normalizedConfig.AllowedRoots.Count,
            normalizedConfig.ReadOnlyPaths.Count,
            normalizedConfig.AllowWriteOperations,
            normalizedConfig.AllowDeleteOperations);

        return Task.CompletedTask;
    }

    public Task<FileOpsBrowseResult> BrowseAsync(string? path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var directory = pathPolicy.ResolveDirectory(runtimeConfig, path);
        var entries = Directory.EnumerateFileSystemEntries(directory.FullPath)
            .Select(CreateEntrySummary)
            .Where(entry => runtimeConfig.AllowHiddenEntries || !entry.IsHidden)
            .OrderByDescending(static entry => entry.IsDirectory)
            .ThenBy(static entry => entry.Name, GetPathComparer())
            .ToList();

        var limitedEntries = entries.Take(runtimeConfig.MaxDirectoryEntries).ToList();
        return Task.FromResult(new FileOpsBrowseResult
        {
            CurrentPath = directory.FullPath,
            CurrentRootPath = directory.RootPath,
            ParentPath = pathPolicy.GetParentPath(directory),
            Breadcrumbs = pathPolicy.BuildBreadcrumbs(directory),
            Entries = limitedEntries,
            CanCreateDirectory = runtimeConfig.AllowWriteOperations && !pathPolicy.IsReadOnlyPath(runtimeConfig, directory.FullPath),
            CanUpload = runtimeConfig.AllowWriteOperations && !pathPolicy.IsReadOnlyPath(runtimeConfig, directory.FullPath),
            IsReadOnly = pathPolicy.IsReadOnlyPath(runtimeConfig, directory.FullPath),
            IsTruncated = entries.Count > limitedEntries.Count,
            EntryLimit = runtimeConfig.MaxDirectoryEntries
        });

        FileOpsEntrySummary CreateEntrySummary(string entryPath)
        {
            var attributes = File.GetAttributes(entryPath);
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                var directoryInfo = new DirectoryInfo(entryPath);
                return new FileOpsEntrySummary
                {
                    Name = directoryInfo.Name,
                    FullPath = directoryInfo.FullName,
                    Kind = FileOpsEntryKind.Directory,
                    LastModifiedAt = directoryInfo.LastWriteTimeUtc,
                    IsHidden = pathPolicy.IsHidden(directoryInfo),
                    IsReadOnly = pathPolicy.IsReadOnlyPath(runtimeConfig, directoryInfo.FullName),
                    CanDelete = runtimeConfig.AllowDeleteOperations && !pathPolicy.IsReadOnlyPath(runtimeConfig, directoryInfo.FullName),
                    CanWriteInto = runtimeConfig.AllowWriteOperations && !pathPolicy.IsReadOnlyPath(runtimeConfig, directoryInfo.FullName)
                };
            }

            var fileInfo = new FileInfo(entryPath);
            return new FileOpsEntrySummary
            {
                Name = fileInfo.Name,
                FullPath = fileInfo.FullName,
                Kind = FileOpsEntryKind.File,
                Extension = fileInfo.Extension,
                SizeBytes = fileInfo.Length,
                LastModifiedAt = fileInfo.LastWriteTimeUtc,
                IsHidden = pathPolicy.IsHidden(fileInfo),
                IsReadOnly = pathPolicy.IsReadOnlyPath(runtimeConfig, fileInfo.FullName),
                CanDownload = fileInfo.Length <= runtimeConfig.MaxDownloadFileSizeBytes,
                CanEditText = fileInfo.Length <= runtimeConfig.MaxTextFileSizeBytes &&
                              runtimeConfig.CanEditExtension(fileInfo.Extension) &&
                              runtimeConfig.AllowWriteOperations &&
                              !pathPolicy.IsReadOnlyPath(runtimeConfig, fileInfo.FullName),
                CanDelete = runtimeConfig.AllowDeleteOperations && !pathPolicy.IsReadOnlyPath(runtimeConfig, fileInfo.FullName)
            };
        }
    }

    public async Task<FileOpsFileTextContent> ReadTextFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var file = pathPolicy.ResolveFile(runtimeConfig, path);
        var readResult = await textInspector.ReadAsync(file.FullPath, runtimeConfig.MaxTextFileSizeBytes, cancellationToken);
        var fileInfo = new FileInfo(file.FullPath);

        return new FileOpsFileTextContent
        {
            FullPath = file.FullPath,
            Name = fileInfo.Name,
            Extension = fileInfo.Extension,
            Content = readResult.Content,
            EncodingName = readResult.EncodingName,
            SizeBytes = fileInfo.Length,
            LastModifiedAt = fileInfo.LastWriteTimeUtc,
            LineCount = readResult.LineCount,
            CanSave = runtimeConfig.AllowWriteOperations &&
                      !pathPolicy.IsReadOnlyPath(runtimeConfig, file.FullPath) &&
                      runtimeConfig.CanEditExtension(fileInfo.Extension),
            CanDownload = fileInfo.Length <= runtimeConfig.MaxDownloadFileSizeBytes,
            IsReadOnly = pathPolicy.IsReadOnlyPath(runtimeConfig, file.FullPath)
        };
    }

    public async Task SaveTextFileAsync(FileOpsTextUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var file = pathPolicy.ResolveFile(runtimeConfig, request.Path);
        if (!runtimeConfig.CanEditExtension(Path.GetExtension(file.FullPath)))
        {
            throw FileOpsOperationException.EditableExtensionRequired(file.FullPath);
        }

        pathPolicy.EnsureEntryWritable(runtimeConfig, file.FullPath);

        var encoding = textInspector.ResolveEncoding(request.EncodingName);
        var bytes = encoding.GetBytes(request.Content ?? string.Empty);
        if (bytes.LongLength > runtimeConfig.MaxTextFileSizeBytes)
        {
            throw FileOpsOperationException.TextFileTooLarge(file.FullPath, runtimeConfig.MaxTextFileSizeBytes.FormatByteSize());
        }

        await File.WriteAllTextAsync(file.FullPath, request.Content ?? string.Empty, encoding, cancellationToken);
        logger.LogInformation("Saved text file via FileOps workspace: {Path}", file.FullPath);
    }

    public Task<string> CreateDirectoryAsync(FileOpsCreateDirectoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var fullPath = pathPolicy.ResolveChildPath(runtimeConfig, request.ParentPath, request.DirectoryName);
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            throw FileOpsOperationException.EntryAlreadyExists(fullPath);
        }

        Directory.CreateDirectory(fullPath);
        logger.LogInformation("Created directory via FileOps workspace: {Path}", fullPath);
        return Task.FromResult(fullPath);
    }

    public Task<FileOpsDeleteResult> DeleteEntryAsync(FileOpsDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var entry = pathPolicy.ResolveExistingEntry(runtimeConfig, request.Path);
        pathPolicy.EnsureEntryDeletable(runtimeConfig, entry);

        if (entry.IsDirectory)
        {
            Directory.Delete(entry.FullPath, request.Recursive);
        }
        else
        {
            File.Delete(entry.FullPath);
        }

        logger.LogInformation("Deleted FileOps entry: {Path}", entry.FullPath);
        return Task.FromResult(new FileOpsDeleteResult
        {
            Path = entry.FullPath,
            EntryKind = entry.IsDirectory ? FileOpsEntryKind.Directory : FileOpsEntryKind.File
        });
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }
}
