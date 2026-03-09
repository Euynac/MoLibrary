using System.Text;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.Framework.UI.UILogging.Models;

/// <summary>
/// 屏幕日志池，负责维护临时日志数据
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
    /// 用指定日志重置缓存
    /// </summary>
    /// <param name="lines">日志集合</param>
    /// <param name="startLineNumber">起始行号（第一行的绝对行号）</param>
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
    /// 追加一条日志
    /// </summary>
    /// <param name="line">日志内容</param>
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
    /// 在缓冲区开头前置日志行
    /// </summary>
    /// <param name="lines">要前置的日志行集合</param>
    /// <param name="startLineNumber">起始行号（第一行的绝对行号）</param>
    public ScreenLogSnapshot Prepend(IEnumerable<string> lines, long startLineNumber)
    {
        lock (_syncRoot)
        {
            var linesList = lines.ToList();
            if (linesList.Count == 0)
            {
                return BuildSnapshotLocked(incrementVersion: false);
            }

            // 将新行添加到开头
            foreach (var line in linesList.AsEnumerable().Reverse())
            {
                if (line is null)
                {
                    continue;
                }

                _rawLines.AddFirst(line);
            }

            // 更新起始行号
            _firstLineNumber = startLineNumber;

            // 清理超出容量的旧日志（从尾部移除）
            while (_rawLines.Count > _maxRetainedLines)
            {
                _rawLines.RemoveLast();
            }

            return BuildSnapshotLocked();
        }
    }

    /// <summary>
    /// 更新筛选器
    /// </summary>
    /// <param name="filter">筛选条件</param>
    public ScreenLogSnapshot ApplyFilter(LogFilterState filter)
    {
        lock (_syncRoot)
        {
            _filter = filter;
            return BuildSnapshotLocked();
        }
    }

    /// <summary>
    /// 获取当前快照
    /// </summary>
    public ScreenLogSnapshot Snapshot()
    {
        lock (_syncRoot)
        {
            return BuildSnapshotLocked(incrementVersion: false);
        }
    }

    /// <summary>
    /// 导出当前日志
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

        // 第一遍：构建所有行并分配缓冲区索引和绝对行号
        foreach (var raw in _rawLines)
        {
            var line = LogLineViewModel.FromRaw(raw);
            line.WithFilter(_filter);
            line.WithPosition(absoluteLineNumber, bufferIndex);
            allLines.Add(line);
            bufferIndex++;
            absoluteLineNumber++;
        }

        // 第二遍：筛选可见行
        var filtered = new List<LogLineViewModel>(_maxDisplayLines);
        foreach (var line in allLines)
        {
            if (ShouldInclude(line))
            {
                filtered.Add(line);
            }
        }

        // 第三遍：为筛选后的行分配筛选索引
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

        // 裁剪到最大显示行数
        if (filtered.Count <= _maxDisplayLines)
        {
            return filtered;
        }

        var skip = filtered.Count - _maxDisplayLines;
        var result = filtered.Skip(skip).ToList();

        // 重新分配筛选索引（如果启用了筛选）
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
