using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Monica.Framework.UI.UILogging.Models;

/// <summary>
/// 日志匹配片段，用于前端高亮
/// </summary>
/// <param name="Start">起始位置</param>
/// <param name="Length">匹配长度</param>
public readonly record struct LogMatchSegment(int Start, int Length);

/// <summary>
/// 日志等级
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
/// 日志筛选状态
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
/// 屏幕日志快照
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
/// 界面展示用的日志行
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
    /// 绝对行号（在原始日志文件中的行号）
    /// </summary>
    public long? AbsoluteLineNumber { get; private set; }

    /// <summary>
    /// 缓冲区索引（在未过滤的缓冲区中的位置，从1开始）
    /// </summary>
    public int? BufferIndex { get; private set; }

    /// <summary>
    /// 过滤后索引（在过滤视图中的位置，从1开始，仅在过滤时有值）
    /// </summary>
    public int? FilteredIndex { get; private set; }

    /// <summary>
    /// 是否为上下文跳转的目标行
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
    /// 设置位置信息
    /// </summary>
    public LogLineViewModel WithPosition(long? absoluteLineNumber, int? bufferIndex, int? filteredIndex = null)
    {
        AbsoluteLineNumber = absoluteLineNumber;
        BufferIndex = bufferIndex;
        FilteredIndex = filteredIndex;
        return this;
    }

    /// <summary>
    /// 标记为上下文目标行
    /// </summary>
    public LogLineViewModel WithContextTarget(bool isTarget)
    {
        IsContextTarget = isTarget;
        return this;
    }

    /// <summary>
    /// 获取格式化的复制文本
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
    /// 获取格式化的时间戳
    /// </summary>
    public string GetFormattedTimestamp()
    {
        return Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "N/A";
    }
}

/// <summary>
/// 日志复制格式
/// </summary>
public enum LogCopyFormat
{
    /// <summary>
    /// 原始完整日志行
    /// </summary>
    Raw,

    /// <summary>
    /// 仅消息内容
    /// </summary>
    MessageOnly,

    /// <summary>
    /// 带时间戳的消息
    /// </summary>
    WithTimestamp,

    /// <summary>
    /// 带行号的完整日志
    /// </summary>
    WithLineNumber
}

/// <summary>
/// 日志文件信息
/// </summary>
/// <param name="FileName">文件名</param>
/// <param name="Size">文件大小</param>
/// <param name="LastModified">最后修改时间</param>
/// <param name="RelativePath">相对路径</param>
public sealed record class LogFileDescriptor(string FileName, long Size, DateTime LastModified, string RelativePath);

/// <summary>
/// 导出结果
/// </summary>
/// <param name="FileName">文件名</param>
/// <param name="ContentType">内容类型</param>
/// <param name="Content">文件内容</param>
public readonly record struct LogExportResult(string FileName, string ContentType, byte[] Content);

/// <summary>
/// 上下文日志读取结果
/// </summary>
/// <param name="Lines">日志行列表</param>
/// <param name="TargetLineNumber">目标行号</param>
/// <param name="StartLineNumber">起始行号</param>
/// <param name="EndLineNumber">结束行号</param>
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
