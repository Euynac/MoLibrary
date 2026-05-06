namespace Monica.AI.Skills.Models;

/// <summary>
/// Agent-facing description of one configured read-only file access root.
/// </summary>
internal sealed record ReadOnlyFileRootInfo(
    string Name,
    string? Description,
    string Path,
    bool Exists);

/// <summary>
/// Result returned by the root-listing tool.
/// </summary>
internal sealed record ReadOnlyFileRootListResult(
    IReadOnlyList<ReadOnlyFileRootInfo> Roots,
    int ResultCount);

/// <summary>
/// Result returned by the bounded file-read tool.
/// </summary>
internal sealed record ReadOnlyFileReadResult
{
    /// <summary>
    /// Name of the configured root used for this read.
    /// </summary>
    public required string RootName { get; init; }

    /// <summary>
    /// Relative path requested by the agent.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Resolved absolute path inside the configured root.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// Whether the file exists.
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// Whether the file is supported as text content.
    /// </summary>
    public required bool IsText { get; init; }

    /// <summary>
    /// One-based line number where returned content starts.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Number of lines returned in this response.
    /// </summary>
    public required int ReturnedLineCount { get; init; }

    /// <summary>
    /// Total line count when the file could be read.
    /// </summary>
    public int? TotalLineCount { get; init; }

    /// <summary>
    /// Returned text content.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// One-based next line to request when more content remains.
    /// </summary>
    public int? NextStartLine { get; init; }

    /// <summary>
    /// Whether another line window can be requested with <see cref="NextStartLine"/>.
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// Whether the returned text was truncated by token budget after line-window selection.
    /// </summary>
    public required bool TokenTruncated { get; init; }

    /// <summary>
    /// Human-readable status or continuation guidance.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// One ripgrep match or context line returned by the search tool.
/// </summary>
internal sealed record ReadOnlyFileSearchLine(
    string Path,
    int LineNumber,
    string Text,
    bool IsMatch,
    bool TextTruncated);

/// <summary>
/// Result returned by the file-content search tool.
/// </summary>
internal sealed record ReadOnlyFileSearchResult(
    string RootName,
    string Pattern,
    string? DirectoryPath,
    string? Glob,
    bool IgnoreCase,
    int ContextLines,
    int Offset,
    int MaxResults,
    IReadOnlyList<ReadOnlyFileSearchLine> Results,
    int ResultCount,
    bool Truncated,
    bool HasMore,
    int? NextOffset,
    string? Message);

/// <summary>
/// One path returned by the file-listing tool.
/// </summary>
internal sealed record ReadOnlyFileListEntry(
    string Path);

/// <summary>
/// Result returned by the file-listing tool.
/// </summary>
internal sealed record ReadOnlyFileListResult(
    string RootName,
    string? DirectoryPath,
    string? Glob,
    int Offset,
    int MaxResults,
    IReadOnlyList<ReadOnlyFileListEntry> Files,
    int ResultCount,
    bool Truncated,
    bool HasMore,
    int? NextOffset,
    string? Message);
