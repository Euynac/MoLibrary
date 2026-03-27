using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Monica.Framework.UI.UILogging.Models;

/// <summary>
/// Log matching fragments for front-end highlighting
/// </summary>
/// <param name="Start">Start position</param>
/// <param name="Length">Match length</param>
public readonly record struct LogMatchSegment(int Start, int Length);

/// <summary>
/// Log level
/// </summary>
public enum LogSeverity
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    Unknown
}

/// <summary>
/// Log filter status
/// </summary>
public sealed class LogFilterState
{
    private LogFilterState()
    {
    }

    public string? Keyword { get; private set; }

    public bool OnlyCapture { get; private set; }

    public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.UtcNow;

    public bool IsActive => !string.IsNullOrWhiteSpace(Keyword);

    public static LogFilterState Disabled => new()
    {
        Keyword = null,
        OnlyCapture = false
    };

    public static LogFilterState Create(string? keyword, bool onlyCapture)
    {
        return new LogFilterState
        {
            Keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword,
            OnlyCapture = onlyCapture,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }

    public LogFilterState WithToggleOnlyCapture(bool onlyCapture)
    {
        return Create(Keyword, onlyCapture);
    }

    public override string ToString()
    {
        return IsActive ? $"Keyword={Keyword}, OnlyCapture={OnlyCapture}" : "Disabled";
    }
}

/// <summary>
/// Screen log snapshot
/// </summary>
public sealed class ScreenLogSnapshot
{
    public required IReadOnlyList<LogLineViewModel> Lines { get; init; }

    public required long Version { get; init; }

    public required int TotalStoredLines { get; init; }

    public required LogFilterState Filter { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>
/// Log lines for interface display
/// </summary>
public sealed class LogLineViewModel
{
    private LogLineViewModel(
        string raw,
        string message,
        string header,
        string levelText,
        LogSeverity severity,
        string thread,
        DateTimeOffset? timestamp)
    {
        Raw = raw;
        Message = message;
        Header = header;
        LevelText = levelText;
        Severity = severity;
        Thread = thread;
        Timestamp = timestamp;
    }

    public string Raw { get; }

    public string Message { get; }

    public string Header { get; }

    public string LevelText { get; }

    public LogSeverity Severity { get; }

    public string Thread { get; }

    public DateTimeOffset? Timestamp { get; }

    public IReadOnlyList<LogMatchSegment>? MatchSegments { get; private set; }

    public bool IsMatch { get; private set; }

    /// <summary>
    /// Absolute line number (line number in the original log file)
    /// </summary>
    public long? AbsoluteLineNumber { get; private set; }

    /// <summary>
    /// Buffer index (position in unfiltered buffer, starting from 1)
    /// </summary>
    public int? BufferIndex { get; private set; }

    /// <summary>
    /// Index after filtering (position in filtered view, starting from 1, only has value when filtering)
    /// </summary>
    public int? FilteredIndex { get; private set; }

    /// <summary>
    /// Whether it is the target line of the context jump
    /// </summary>
    public bool IsContextTarget { get; private set; }

    public static LogLineViewModel FromRaw(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return new LogLineViewModel(string.Empty, string.Empty, string.Empty, string.Empty, LogSeverity.Unknown, string.Empty, null);
        }

        var parsed = LogLineParser.Parse(rawLine);
        return new LogLineViewModel(
            rawLine,
            parsed.Message,
            parsed.Header,
            parsed.LevelText,
            parsed.Severity,
            parsed.Thread,
            parsed.Timestamp);
    }

    public LogLineViewModel WithFilter(LogFilterState filter)
    {
        if (!filter.IsActive)
        {
            MatchSegments = null;
            IsMatch = false;
            return this;
        }

        var keyword = filter.Keyword!;
        var segments = Highlight(Message, keyword);
        MatchSegments = segments;
        IsMatch = segments is { Count: > 0 };
        return this;
    }

    private static IReadOnlyList<LogMatchSegment>? Highlight(string message, string keyword)
    {
        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(keyword))
        {
            return null;
        }

        var list = ImmutableArray.CreateBuilder<LogMatchSegment>();
        var comparison = CultureInfo.InvariantCulture.CompareInfo;
        var index = 0;
        while (index < message.Length)
        {
            var matchIndex = comparison.IndexOf(message, keyword, index, CompareOptions.IgnoreCase);
            if (matchIndex < 0)
            {
                break;
            }

            list.Add(new LogMatchSegment(matchIndex, keyword.Length));
            index = matchIndex + keyword.Length;
        }

        return list.Count == 0 ? null : list.ToImmutable();
    }

    /// <summary>
    /// Set location
    /// </summary>
    public LogLineViewModel WithPosition(long? absoluteLineNumber, int? bufferIndex, int? filteredIndex = null)
    {
        AbsoluteLineNumber = absoluteLineNumber;
        BufferIndex = bufferIndex;
        FilteredIndex = filteredIndex;
        return this;
    }

    /// <summary>
    /// Mark as contextual target row
    /// </summary>
    public LogLineViewModel WithContextTarget(bool isTarget)
    {
        IsContextTarget = isTarget;
        return this;
    }

    /// <summary>
    /// Get formatted copied text
    /// </summary>
    public string GetCopyText(LogCopyFormat format)
    {
        return format switch
        {
            LogCopyFormat.Raw => Raw,
            LogCopyFormat.MessageOnly => Message,
            LogCopyFormat.WithTimestamp => Timestamp.HasValue
                ? $"[{Timestamp.Value:yyyy-MM-dd HH:mm:ss.fff}] {Message}"
                : Message,
            LogCopyFormat.WithLineNumber => AbsoluteLineNumber.HasValue
                ? $"Line {AbsoluteLineNumber}: {Raw}"
                : Raw,
            _ => Raw
        };
    }

    /// <summary>
    /// Get formatted timestamp
    /// </summary>
    public string GetFormattedTimestamp()
    {
        return Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "N/A";
    }
}

/// <summary>
/// Log replication format
/// </summary>
public enum LogCopyFormat
{
    /// <summary>
    /// Original full log line
    /// </summary>
    Raw,

    /// <summary>
    /// Message content only
    /// </summary>
    MessageOnly,

    /// <summary>
    /// timestamped messages
    /// </summary>
    WithTimestamp,

    /// <summary>
    /// Full log with line numbers
    /// </summary>
    WithLineNumber
}

/// <summary>
/// Log file information
/// </summary>
/// <param name="FileName">File name</param>
/// <param name="Size">File size</param>
/// <param name="LastModified">Last modified time</param>
/// <param name="RelativePath">Relative path</param>
public sealed record class LogFileDescriptor(string FileName, long Size, DateTime LastModified, string RelativePath);

/// <summary>
/// Export results
/// </summary>
/// <param name="FileName">File name</param>
/// <param name="ContentType">Content Type</param>
/// <param name="Content">File content</param>
public readonly record struct LogExportResult(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Context log reading results
/// </summary>
/// <param name="Lines">Log line list</param>
/// <param name="TargetLineNumber">Target line number</param>
/// <param name="StartLineNumber">Start line number</param>
/// <param name="EndLineNumber">End line number</param>
public readonly record struct ContextLogResult(
    IReadOnlyList<LogLineViewModel> Lines,
    long TargetLineNumber,
    long StartLineNumber,
    long EndLineNumber);

internal static class LogLineParser
{
    private static readonly Regex LogRegex = new(
        @"^\[(?<timestamp>.+?)\s+(?<level>[A-Z]+)\s+(?<thread>[^\]]*?)\]\s*(?<message>.*)$",
        RegexOptions.Compiled);

    public static ParsedLogLine Parse(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return new ParsedLogLine(rawLine, string.Empty, string.Empty, LogSeverity.Unknown, string.Empty, null, string.Empty);
        }

        var match = LogRegex.Match(rawLine);
        if (!match.Success)
        {
            return new ParsedLogLine(rawLine, string.Empty, rawLine, LogSeverity.Unknown, string.Empty, null, rawLine);
        }

        var timestampText = match.Groups["timestamp"].Value;
        var levelText = match.Groups["level"].Value;
        var thread = match.Groups["thread"].Value;
        var message = match.Groups["message"].Value;

        DateTimeOffset? timestamp = null;
        if (DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedTimestamp))
        {
            timestamp = parsedTimestamp;
        }

        return new ParsedLogLine(
            rawLine,
            $"[{timestampText} {levelText} {thread}]",
            message,
            MapSeverity(levelText),
            levelText,
            timestamp,
            thread);
    }

    private static LogSeverity MapSeverity(string? levelText)
    {
        if (string.IsNullOrWhiteSpace(levelText))
        {
            return LogSeverity.Unknown;
        }

        return levelText.ToUpperInvariant() switch
        {
            "VRB" or "TRC" or "TRACE" => LogSeverity.Trace,
            "DBG" or "DEBUG" => LogSeverity.Debug,
            "INF" or "INFO" or "INFORMATION" => LogSeverity.Information,
            "WRN" or "WARN" or "WARNING" => LogSeverity.Warning,
            "ERR" or "ERROR" => LogSeverity.Error,
            "FTL" or "CRT" or "CRITICAL" or "FATAL" => LogSeverity.Critical,
            _ => LogSeverity.Unknown
        };
    }
}

internal readonly record struct ParsedLogLine(
    string Raw,
    string Header,
    string Message,
    LogSeverity Severity,
    string LevelText,
    DateTimeOffset? Timestamp,
    string Thread);
