# MoProfiling - 性能监测模块

基于 .NET EventCounters 的性能监测实现，提供实时的 CPU 和内存使用情况监控。

## 功能特性

- **实时监控**: 使用 EventCounters 实时收集性能数据
- **CPU 使用率**: 监控当前进程的 CPU 使用率
- **内存使用情况**: 详细的内存使用信息，包括工作集、GC堆、GC次数等
- **异步接口**: 提供异步方法以避免阻塞主线程
- **资源管理**: 实现 IDisposable 接口，确保资源正确释放

## 使用方法

### 基本用法

```csharp
using MoLibrary.Framework.Features.MoProfiling;

// 创建性能监测实例
using var profiling = new MoProfiling();

// 获取 CPU 使用率
var cpuUsage = await profiling.GetCpuUsageAsync();
Console.WriteLine($"CPU 使用率: {cpuUsage}");

// 获取内存使用情况
var memoryUsage = await profiling.GetMemoryUsageAsync();
Console.WriteLine($"内存使用: {memoryUsage}");
```

### 依赖注入使用

```csharp
// 在 Program.cs 或 Startup.cs 中注册服务
services.AddSingleton<IMoProfiling, MoProfiling>();

// 在控制器或服务中使用
public class PerformanceController : ControllerBase
{
    private readonly IMoProfiling _profiling;

    public PerformanceController(IMoProfiling profiling)
    {
        _profiling = profiling;
    }

    [HttpGet("cpu")]
    public async Task<string> GetCpuUsage()
    {
        return await _profiling.GetCpuUsageAsync();
    }

    [HttpGet("memory")]
    public async Task<string> GetMemoryUsage()
    {
        return await _profiling.GetMemoryUsageAsync();
    }
}
```

## 监控的性能指标

### CPU 相关
- **cpu-usage**: CPU 使用率百分比

### 内存相关
- **working-set**: 工作集内存大小
- **gc-heap-size**: GC 堆大小
- **gen-0-gc-count**: Generation 0 GC 次数
- **gen-1-gc-count**: Generation 1 GC 次数
- **gen-2-gc-count**: Generation 2 GC 次数
- **alloc-rate**: 内存分配速率
- **gc-committed**: GC 已提交的内存

## 实现原理

本实现基于 .NET EventCounters API，通过以下方式工作：

1. **EventListener**: 继承 `EventListener` 类来监听系统事件
2. **System.Runtime**: 专门监听 `System.Runtime` EventSource 的性能计数器
3. **实时收集**: 每秒收集一次性能数据
4. **缓存机制**: 使用 `ConcurrentDictionary` 缓存最新的性能指标

## 注意事项

1. **初始化延迟**: EventCounters 需要一定时间才能开始提供数据，首次调用可能返回默认值
2. **.NET 版本**: 需要 .NET Core 3.0 或更高版本
3. **性能影响**: 监控本身会消耗少量系统资源
4. **资源释放**: 请确保正确释放 `MoProfiling` 实例以避免内存泄漏

## 参考资料

- [EventCounters in .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/event-counters)
- [Available counters in .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/available-counters)
- [RuntimeEventSource](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/RuntimeEventSource.cs) 