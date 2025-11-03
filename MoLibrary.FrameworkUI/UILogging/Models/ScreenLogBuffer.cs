using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using MoLibrary.FrameworkUI.Modules;

namespace MoLibrary.FrameworkUI.UILogging.Models;

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

    public int MaxDisplayLines => _maxDisplayLines;

    public LogFilterState CurrentFilter => _filter;

    /// <summary>
    /// 用指定日志重置缓存
    /// </summary>
    /// <param name="lines">日志集合</param>
    public ScreenLogSnapshot Reset(IEnumerable<string> lines)
    {
        lock (_syncRoot)
        {
            _rawLines.Clear();
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
        var filtered = new List<LogLineViewModel>(_maxDisplayLines);
        foreach (var raw in _rawLines)
        {
            var line = LogLineViewModel.FromRaw(raw);
            line.WithFilter(_filter);
            if (ShouldInclude(line))
            {
                filtered.Add(line);
            }
        }

        if (filtered.Count <= _maxDisplayLines)
        {
            return filtered;
        }

        var skip = filtered.Count - _maxDisplayLines;
        return filtered.Skip(skip).ToList();
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
        }
    }
}
