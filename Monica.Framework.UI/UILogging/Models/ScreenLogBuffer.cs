using System.Text;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.Framework.UI.UILogging.Models;

/// <summary>
/// Screen log pool, responsible for maintaining temporary log data
/// </summary>
public sealed class ScreenLogBuffer(IOptions<ModuleLoggingUIOption> options)
{
    private readonly LinkedList<string> _rawLines = new();
    private readonly object _syncRoot = new();
    private readonly int _maxDisplayLines = Math.Max(100, options.Value.MaxDisplayLines);
    private readonly int _maxRetainedLines = Math.Max(200, options.Value.MaxDisplayLines * 2);

    private LogFilterState _filter = LogFilterState.Disabled;
    private long _version;
    private long _firstLineNumber = 1; // 缓冲区第一行的绝对行号
    private int _trimmedCount; // 记录已裁剪的行数

    public int MaxDisplayLines => _maxDisplayLines;

    public LogFilterState CurrentFilter => _filter;

    /// <summary>
    /// Reset cache with specified log
    /// </summary>
    /// <param name="lines">Log collection</param>
    /// <param name="startLineNumber">Start line number (absolute line number of the first line)</param>
    public ScreenLogSnapshot Reset(IEnumerable<string> lines, long startLineNumber = 1)
    {
        lock (_syncRoot)
        {
            _rawLines.Clear();
            _firstLineNumber = startLineNumber;
            _trimmedCount = 0;

            foreach (var line in lines)
            {
                if (line is null)
                {
                    continue;
                }

                _rawLines.AddLast(line);
            }

            TrimRawIfNeeded();
            return BuildSnapshotLocked();
        }
    }

    /// <summary>
    /// Append a log
    /// </summary>
    /// <param name="line">Log content</param>
    public ScreenLogSnapshot Append(string line)
    {
        lock (_syncRoot)
        {
            _rawLines.AddLast(line);
            TrimRawIfNeeded();
            return BuildSnapshotLocked();
        }
    }

    /// <summary>
    /// Prepend log line at beginning of buffer
    /// </summary>
    /// <param name="lines">The set of log lines to be prepended</param>
    /// <param name="startLineNumber">Start line number (absolute line number of the first line)</param>
    public ScreenLogSnapshot Prepend(IEnumerable<string> lines, long startLineNumber)
    {
        lock (_syncRoot)
        {
            var linesList = lines.ToList();
            if (linesList.Count == 0)
            {
                return BuildSnapshotLocked(incrementVersion: false);
            }

            // Add new line to beginning
            foreach (var line in linesList.AsEnumerable().Reverse())
            {
                if (line is null)
                {
                    continue;
                }

                _rawLines.AddFirst(line);
            }

            // Update starting line number
            _firstLineNumber = startLineNumber;

            // Clean up old logs that exceed capacity (remove from the tail)
            while (_rawLines.Count > _maxRetainedLines)
            {
                _rawLines.RemoveLast();
            }

            return BuildSnapshotLocked();
        }
    }

    /// <summary>
    /// Update filter
    /// </summary>
    /// <param name="filter">Filter conditions</param>
    public ScreenLogSnapshot ApplyFilter(LogFilterState filter)
    {
        lock (_syncRoot)
        {
            _filter = filter;
            return BuildSnapshotLocked();
        }
    }

    /// <summary>
    /// Get current snapshot
    /// </summary>
    public ScreenLogSnapshot Snapshot()
    {
        lock (_syncRoot)
        {
            return BuildSnapshotLocked(incrementVersion: false);
        }
    }

    /// <summary>
    /// Export current log
    /// </summary>
    public string ExportCurrent()
    {
        lock (_syncRoot)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"# Exported at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            if (_filter.IsActive)
            {
                builder.AppendLine($"# Filter: Keyword={_filter.Keyword}, OnlyCapture={_filter.OnlyCapture}");
            }
            builder.AppendLine($"# Total Stored Lines: {_rawLines.Count}");
            builder.AppendLine();

            foreach (var line in _rawLines)
            {
                builder.AppendLine(line);
            }

            return builder.ToString();
        }
    }

    private ScreenLogSnapshot BuildSnapshotLocked(bool incrementVersion = true)
    {
        var lines = BuildVisibleLines();
        var version = incrementVersion ? ++_version : _version;

        return new ScreenLogSnapshot
        {
            Lines = lines,
            Version = version,
            TotalStoredLines = _rawLines.Count,
            Filter = _filter,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    private IReadOnlyList<LogLineViewModel> BuildVisibleLines()
    {
        var allLines = new List<LogLineViewModel>(_rawLines.Count);
        var bufferIndex = 1;
        var absoluteLineNumber = _firstLineNumber;

        // First pass: build all lines and assign buffer index and absolute line number
        foreach (var raw in _rawLines)
        {
            var line = LogLineViewModel.FromRaw(raw);
            line.WithFilter(_filter);
            line.WithPosition(absoluteLineNumber, bufferIndex);
            allLines.Add(line);
            bufferIndex++;
            absoluteLineNumber++;
        }

        // Second pass: filter visible rows
        var filtered = new List<LogLineViewModel>(_maxDisplayLines);
        foreach (var line in allLines)
        {
            if (ShouldInclude(line))
            {
                filtered.Add(line);
            }
        }

        // Third pass: Assign filter index to filtered rows
        if (_filter.IsActive && _filter.OnlyCapture)
        {
            var filteredIndex = 1;
            foreach (var line in filtered)
            {
                if (line.IsMatch)
                {
                    line.WithPosition(line.AbsoluteLineNumber, line.BufferIndex, filteredIndex);
                    filteredIndex++;
                }
            }
        }

        // Crop to maximum number of displayed lines
        if (filtered.Count <= _maxDisplayLines)
        {
            return filtered;
        }

        var skip = filtered.Count - _maxDisplayLines;
        var result = filtered.Skip(skip).ToList();

        // Reassign filter index (if filtering is enabled)
        if (_filter.IsActive && _filter.OnlyCapture)
        {
            var filteredIndex = 1;
            foreach (var line in result)
            {
                if (line.IsMatch)
                {
                    line.WithPosition(line.AbsoluteLineNumber, line.BufferIndex, filteredIndex);
                    filteredIndex++;
                }
            }
        }

        return result;
    }

    private bool ShouldInclude(LogLineViewModel line)
    {
        if (!_filter.IsActive)
        {
            return true;
        }

        if (line.IsMatch)
        {
            return true;
        }

        return !_filter.OnlyCapture;
    }

    private void TrimRawIfNeeded()
    {
        while (_rawLines.Count > _maxRetainedLines)
        {
            _rawLines.RemoveFirst();
            _trimmedCount++;
            _firstLineNumber++;
        }
    }
}
