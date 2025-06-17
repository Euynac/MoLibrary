using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace MoLibrary.Framework.Features.MoProfiling;

/// <summary>
/// 程序性能监测实现类
/// 使用 EventCounters 监控 CPU 和内存使用情况
/// </summary>
public class MoProfiling : IMoProfiling, IDisposable
{
    private readonly RuntimeMetricsEventListener _eventListener;
    private bool _disposed;

    /// <summary>
    /// 初始化性能监测实例
    /// </summary>
    public MoProfiling()
    {
        _eventListener = new RuntimeMetricsEventListener();
    }

    public Task<string> GetCpuUsageAsync()
    {
        var cpuUsage = _eventListener.GetCpuUsage();
        return Task.FromResult($"{cpuUsage:F2}%");
    }

    public Task<double> GetMemoryUsageAsync()
    {
        var memoryUsage = _eventListener.GetMemoryUsage();
        return Task.FromResult(memoryUsage);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _eventListener?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 运行时指标事件监听器
/// 监听 System.Runtime EventSource 的性能计数器
/// </summary>
internal class RuntimeMetricsEventListener : EventListener
{
    private readonly ConcurrentDictionary<string, double> _counters = new();
    private volatile bool _isEnabled;

    /// <summary>
    /// 初始化事件监听器
    /// </summary>
    public RuntimeMetricsEventListener()
    {
    }

    /// <summary>
    /// 当 EventSource 创建时被调用
    /// </summary>
    /// <param name="eventSource">创建的事件源</param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // 只监听 System.Runtime 事件源
        if (eventSource.Name.Equals("System.Runtime"))
        {
            // 启用详细事件级别，每1秒收集一次计数器数据
            EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All, new Dictionary<string, string>()
            {
                ["EventCounterIntervalSec"] = "1"
            });
            _isEnabled = true;
        }
    }

    /// <summary>
    /// 当事件被写入时被调用
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // 只处理 EventCounters 事件
        if (!eventData.EventName.Equals("EventCounters"))
        {
            return;
        }

        // 解析事件负载中的计数器数据
        for (int i = 0; i < eventData.Payload.Count; ++i)
        {
            if (eventData.Payload[i] is IDictionary<string, object> eventPayload)
            {
                var (counterName, counterValue) = GetCounterData(eventPayload);
                if (!string.IsNullOrEmpty(counterName) && counterValue.HasValue)
                {
                    _counters.AddOrUpdate(counterName, counterValue.Value, (key, oldValue) => counterValue.Value);
                }
            }
        }
    }

    /// <summary>
    /// 从事件负载中提取计数器名称和值
    /// </summary>
    /// <param name="eventPayload">事件负载</param>
    /// <returns>计数器名称和值的元组</returns>
    private static (string counterName, double? counterValue) GetCounterData(IDictionary<string, object> eventPayload)
    {
        var counterName = "";
        double? counterValue = null;

        // 获取计数器名称
        if (eventPayload.TryGetValue("Name", out object nameValue))
        {
            counterName = nameValue.ToString();
        }

        // 尝试获取计数器值（不同类型的计数器使用不同的字段）
        if (eventPayload.TryGetValue("Mean", out object meanValue))
        {
            if (double.TryParse(meanValue.ToString(), out double mean))
                counterValue = mean;
        }
        else if (eventPayload.TryGetValue("Increment", out object incrementValue))
        {
            if (double.TryParse(incrementValue.ToString(), out double increment))
                counterValue = increment;
        }
        else if (eventPayload.TryGetValue("Count", out object countValue))
        {
            if (double.TryParse(countValue.ToString(), out double count))
                counterValue = count;
        }

        return (counterName, counterValue);
    }

    /// <summary>
    /// 获取CPU使用率
    /// </summary>
    /// <returns>CPU使用率百分比</returns>
    public double GetCpuUsage()
    {
        // 尝试获取CPU使用率计数器
        if (_counters.TryGetValue("cpu-usage", out double cpuUsage))
        {
            return cpuUsage;
        }

        // 如果没有直接的CPU使用率，尝试通过其他计数器计算
        // 注意：在某些版本的.NET中，可能需要使用不同的计数器名称
        return 0.0; // 默认返回0，表示无法获取CPU使用率
    }

    /// <summary>
    /// 获取内存使用量
    /// </summary>
    /// <returns>内存使用量MB</returns>
    public double GetMemoryUsage()
    {
        // 优先返回工作集内存
        if (_counters.TryGetValue("working-set", out double workingSet))
        {
            return workingSet;
        }

        // 默认返回0，表示无法获取内存使用量
        return 0.0;
    }

    /// <summary>
    /// 获取内存使用信息
    /// </summary>
    /// <returns>格式化的内存使用信息字符串</returns>
    public string GetMemoryInfo()
    {
        var info = new List<string>();

        // 工作集内存
        if (_counters.TryGetValue("working-set", out double workingSet))
        {
            info.Add($"工作集: {FormatBytes(workingSet)}");
        }

        // GC堆内存
        if (_counters.TryGetValue("gc-heap-size", out double gcHeapSize))
        {
            info.Add($"GC堆: {FormatBytes(gcHeapSize)}");
        }

        // Gen 0 GC次数
        if (_counters.TryGetValue("gen-0-gc-count", out double gen0GcCount))
        {
            info.Add($"Gen0 GC: {gen0GcCount:F0}次");
        }

        // Gen 1 GC次数
        if (_counters.TryGetValue("gen-1-gc-count", out double gen1GcCount))
        {
            info.Add($"Gen1 GC: {gen1GcCount:F0}次");
        }

        // Gen 2 GC次数
        if (_counters.TryGetValue("gen-2-gc-count", out double gen2GcCount))
        {
            info.Add($"Gen2 GC: {gen2GcCount:F0}次");
        }

        // 分配的内存
        if (_counters.TryGetValue("alloc-rate", out double allocRate))
        {
            info.Add($"分配速率: {FormatBytes(allocRate)}/s");
        }

        // 活跃对象数量
        if (_counters.TryGetValue("gc-committed", out double gcCommitted))
        {
            info.Add($"GC已提交: {FormatBytes(gcCommitted)}");
        }

        return info.Count > 0 ? string.Join(", ", info) : "内存信息不可用";
    }

    /// <summary>
    /// 格式化字节数为可读格式
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化的字符串</returns>
    private static string FormatBytes(double bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = (decimal)bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:N1}{suffixes[counter]}";
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public override void Dispose()
    {
        _counters.Clear();
        base.Dispose();
    }
}