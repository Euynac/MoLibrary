using Monica.Tool.General;

namespace Monica.DevOps.FileOps.Models;

public enum FileOpsEntryKind
{
    Directory,
    File
}

public class FileOpsPathSegment
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;
}

public class FileOpsEntrySummary
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public FileOpsEntryKind Kind { get; set; }

    public string Extension { get; set; } = string.Empty;

    public long? SizeBytes { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    public bool IsHidden { get; set; }

    public bool IsReadOnly { get; set; }

    public bool CanDownload { get; set; }

    public bool CanEditText { get; set; }

    public bool CanDelete { get; set; }

    public bool CanWriteInto { get; set; }

    public bool IsDirectory => Kind == FileOpsEntryKind.Directory;

    public string SizeDisplay => SizeBytes.HasValue ? SizeBytes.Value.FormatByteSize() : "-";
}

public class FileOpsBrowseResult
{
    public string CurrentPath { get; set; } = string.Empty;

    public string CurrentRootPath { get; set; } = string.Empty;

    public string? ParentPath { get; set; }

    public List<FileOpsPathSegment> Breadcrumbs { get; set; } = [];

    public List<FileOpsEntrySummary> Entries { get; set; } = [];

    public bool CanCreateDirectory { get; set; }

    public bool CanUpload { get; set; }

    public bool IsReadOnly { get; set; }

    public bool IsTruncated { get; set; }

    public int EntryLimit { get; set; }
}

public class FileOpsFileTextContent
{
    public string FullPath { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string EncodingName { get; set; } = "utf-8";

    public long SizeBytes { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    public int LineCount { get; set; }

    public bool CanSave { get; set; }

    public bool CanDownload { get; set; }

    public bool IsReadOnly { get; set; }
}

public class FileOpsTextUpdateRequest
{
    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string EncodingName { get; set; } = "utf-8";
}

public class FileOpsCreateDirectoryRequest
{
    public string ParentPath { get; set; } = string.Empty;

    public string DirectoryName { get; set; } = string.Empty;
}

public class FileOpsDeleteRequest
{
    public string Path { get; set; } = string.Empty;

    public bool Recursive { get; set; }
}

public sealed record FileOpsUploadPayload(string FileName, long Length, Stream Content);

public class FileOpsUploadResult
{
    public string DirectoryPath { get; set; } = string.Empty;

    public bool Overwrite { get; set; }

    public List<FileOpsEntrySummary> UploadedEntries { get; set; } = [];
}

public class FileOpsDeleteResult
{
    public string Path { get; set; } = string.Empty;

    public FileOpsEntryKind EntryKind { get; set; }
}

public class FileOpsDownloadDescriptor
{
    public string FullPath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long SizeBytes { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    public Stream ContentStream { get; set; } = Stream.Null;
}
