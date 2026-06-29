using System.IO.Compression;
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
    private const string ARCHIVE_CONTENT_TYPE = "application/zip";
    private const string ARCHIVE_EXTENSION = ".zip";
    private const int ARCHIVE_STREAM_BUFFER_SIZE = 128 * 1024;

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

    public async Task<FileOpsDownloadDescriptor> OpenArchiveDownloadAsync(
        FileOpsArchiveDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var paths = GetDistinctPaths(request.Paths);
        if (paths.Count == 0)
        {
            throw FileOpsOperationException.SelectionRequired();
        }

        var archivePath = Path.Combine(Path.GetTempPath(), $"monica-file-ops-{Guid.NewGuid():N}{ARCHIVE_EXTENSION}");
        try
        {
            var archiveEntryCount = await CreateArchiveAsync(archivePath, runtimeConfig, paths, cancellationToken);
            if (archiveEntryCount == 0)
            {
                throw FileOpsOperationException.SelectionRequired();
            }

            var archiveInfo = new FileInfo(archivePath);
            var stream = new FileStream(archivePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = ARCHIVE_STREAM_BUFFER_SIZE,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
            });

            logger.LogInformation(
                "Created FileOps archive download {ArchiveName} with {EntryCount} entries and {SizeBytes} bytes.",
                request.ArchiveName,
                archiveEntryCount,
                archiveInfo.Length);

            return new FileOpsDownloadDescriptor
            {
                FullPath = archivePath,
                FileName = NormalizeArchiveFileName(request.ArchiveName),
                ContentType = ARCHIVE_CONTENT_TYPE,
                SizeBytes = archiveInfo.Length,
                LastModifiedAt = DateTimeOffset.UtcNow,
                ContentStream = stream
            };
        }
        catch
        {
            TryDeleteTemporaryArchive(archivePath);
            throw;
        }
    }

    private async Task<int> CreateArchiveAsync(
        string archivePath,
        FileOpsRuntimeConfig runtimeConfig,
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken)
    {
        var usedArchivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entryCount = 0;

        await using var archiveStream = new FileStream(archivePath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = ARCHIVE_STREAM_BUFFER_SIZE,
            Options = FileOptions.Asynchronous
        });
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = pathPolicy.ResolveExistingEntry(runtimeConfig, path);
            var archiveRootPath = ReserveArchivePath(pathPolicy.GetLocationName(entry.FullPath), usedArchivePaths, entry.IsDirectory);

            if (entry.IsDirectory)
            {
                entryCount += await AddDirectoryToArchiveAsync(
                    archive,
                    runtimeConfig,
                    new DirectoryInfo(entry.FullPath),
                    archiveRootPath,
                    usedArchivePaths,
                    cancellationToken);
            }
            else
            {
                await AddFileToArchiveAsync(
                    archive,
                    runtimeConfig,
                    new FileInfo(entry.FullPath),
                    archiveRootPath,
                    cancellationToken);
                entryCount++;
            }
        }

        return entryCount;
    }

    private async Task<int> AddDirectoryToArchiveAsync(
        ZipArchive archive,
        FileOpsRuntimeConfig runtimeConfig,
        DirectoryInfo directoryInfo,
        string archiveDirectoryPath,
        ISet<string> usedArchivePaths,
        CancellationToken cancellationToken)
    {
        var entryCount = 1;
        archive.CreateEntry(EnsureArchiveDirectoryPath(archiveDirectoryPath), CompressionLevel.NoCompression);

        foreach (var child in directoryInfo.EnumerateFileSystemInfos().OrderByDescending(static child => child is DirectoryInfo).ThenBy(static child => child.Name, GetPathComparer()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!runtimeConfig.AllowHiddenEntries && pathPolicy.IsHidden(child))
            {
                continue;
            }

            var childArchivePath = ReserveArchivePath($"{archiveDirectoryPath}/{child.Name}", usedArchivePaths, child is DirectoryInfo);
            if (child is DirectoryInfo childDirectory)
            {
                entryCount += await AddDirectoryToArchiveAsync(
                    archive,
                    runtimeConfig,
                    childDirectory,
                    childArchivePath,
                    usedArchivePaths,
                    cancellationToken);
            }
            else if (child is FileInfo childFile)
            {
                await AddFileToArchiveAsync(archive, runtimeConfig, childFile, childArchivePath, cancellationToken);
                entryCount++;
            }
        }

        return entryCount;
    }

    private static async Task AddFileToArchiveAsync(
        ZipArchive archive,
        FileOpsRuntimeConfig runtimeConfig,
        FileInfo fileInfo,
        string archiveFilePath,
        CancellationToken cancellationToken)
    {
        fileInfo.Refresh();
        if (fileInfo.Length > runtimeConfig.MaxDownloadFileSizeBytes)
        {
            throw FileOpsOperationException.DownloadTooLarge(fileInfo.FullName, runtimeConfig.MaxDownloadFileSizeBytes.FormatByteSize());
        }

        var archiveEntry = archive.CreateEntry(NormalizeArchivePath(archiveFilePath), CompressionLevel.Fastest);
        archiveEntry.LastWriteTime = fileInfo.LastWriteTimeUtc;

        await using var sourceStream = new FileStream(fileInfo.FullName, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite,
            BufferSize = ARCHIVE_STREAM_BUFFER_SIZE,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        await using var archiveEntryStream = archiveEntry.Open();
        await sourceStream.CopyToAsync(archiveEntryStream, cancellationToken);
    }

    private static List<string> GetDistinctPaths(IEnumerable<string>? paths)
    {
        return (paths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(GetPathComparer())
            .ToList();
    }

    private static string NormalizeArchiveFileName(string? archiveName)
    {
        var fileName = string.IsNullOrWhiteSpace(archiveName)
            ? $"file-ops-selection-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}{ARCHIVE_EXTENSION}"
            : Path.GetFileName(archiveName.Trim());

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"file-ops-selection-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}{ARCHIVE_EXTENSION}";
        }

        return fileName.EndsWith(ARCHIVE_EXTENSION, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}{ARCHIVE_EXTENSION}";
    }

    private static string ReserveArchivePath(string archivePath, ISet<string> usedArchivePaths, bool isDirectory)
    {
        var normalizedPath = NormalizeArchivePath(archivePath);
        var uniquePath = normalizedPath;
        var index = 2;
        while (!usedArchivePaths.Add(isDirectory ? EnsureArchiveDirectoryPath(uniquePath) : uniquePath))
        {
            uniquePath = BuildNumberedArchivePath(normalizedPath, index++, isDirectory);
        }

        return uniquePath;
    }

    private static string BuildNumberedArchivePath(string archivePath, int index, bool isDirectory)
    {
        var trimmedPath = archivePath.TrimEnd('/');
        var lastSlashIndex = trimmedPath.LastIndexOf('/');
        var directoryPath = lastSlashIndex >= 0 ? trimmedPath[..(lastSlashIndex + 1)] : string.Empty;
        var entryName = lastSlashIndex >= 0 ? trimmedPath[(lastSlashIndex + 1)..] : trimmedPath;
        var extension = isDirectory ? string.Empty : Path.GetExtension(entryName);
        var nameWithoutExtension = string.IsNullOrEmpty(extension) ? entryName : entryName[..^extension.Length];

        return $"{directoryPath}{nameWithoutExtension} ({index}){extension}";
    }

    private static string EnsureArchiveDirectoryPath(string archivePath)
    {
        return NormalizeArchivePath(archivePath).TrimEnd('/') + "/";
    }

    private static string NormalizeArchivePath(string archivePath)
    {
        var normalizedPath = archivePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');

        return string.IsNullOrWhiteSpace(normalizedPath)
            ? "selection"
            : normalizedPath;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }

    private static void TryDeleteTemporaryArchive(string archivePath)
    {
        try
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
        catch
        {
            // Best-effort cleanup only. A failed delete should not hide the original download error.
        }
    }
}
