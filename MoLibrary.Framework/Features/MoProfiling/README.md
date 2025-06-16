# MoProfiling - 程序性能监测

MoProfiling提供了一套完整的程序性能监测解决方案，可以监测CPU使用率、内存占用、线程数量、句柄数量等关键指标。

## 功能特性

- **CPU监测**: 获取当前进程的CPU使用率
- **内存监测**: 获取工作集内存、私有内存、虚拟内存、GC内存等详细信息
- **系统信息**: 获取系统总内存、可用内存等系统级信息
- **进程指标**: 监测线程数量、句柄数量、进程运行时间等
- **连续监控**: 支持定时获取性能快照，实时监控程序状态
- **性能分析**: 提供方法执行前后的性能对比分析

## 快速开始

### 1. 服务注册

```csharp
// 在 Startup.cs 或 Program.cs 中注册服务
services.AddMoProfiling();

// 或者注册为其他生命周期
services.AddMoProfilingTransient();
services.AddMoProfilingScoped();
```

### 2. 基本使用

```csharp
public class MyService
{
    private readonly IMoProfiling _profiling;

    public MyService(IMoProfiling profiling)
    {
        _profiling = profiling;
    }

    public async Task GetPerformanceInfo()
    {
        // 获取CPU使用率
        var cpuUsage = await _profiling.GetCpuUsageAsync();
        Console.WriteLine($"CPU使用率: {cpuUsage:F1}%");

        // 获取内存使用情况
        var memoryInfo = await _profiling.GetMemoryUsageAsync();
        Console.WriteLine($"内存使用: {memoryInfo.ToFormattedString()}");

        // 获取快速状态信息
        var quickStatus = await _profiling.GetQuickStatusAsync();
        Console.WriteLine($"快速状态: {quickStatus}");

        // 获取完整性能快照
        var snapshot = await _profiling.GetPerformanceSnapshotAsync();
        Console.WriteLine($"性能快照: {snapshot.ToFormattedString()}");
    }
}
```

### 3. 连续监控

```csharp
public async Task StartContinuousMonitoring()
{
    // 每5秒获取一次性能数据
    await _profiling.StartMonitoringAsync(TimeSpan.FromSeconds(5), snapshot =>
    {
        Console.WriteLine($"监控数据: {snapshot.ToFormattedString()}");
        
        // 可以在这里添加报警逻辑
        if (snapshot.CpuUsage > 80)
        {
            Console.WriteLine("警告：CPU使用率过高！");
        }
        
        if (snapshot.MemoryUsage.WorkingSetMemory > 1000) // 1GB
        {
            Console.WriteLine("警告：内存使用量过高！");
        }
    });

    // 在适当的时候停止监控
    // _profiling.StopMonitoring();
}
```

### 4. 性能分析

```csharp
public async Task AnalyzeMethodPerformance()
{
    // 分析异步方法的性能影响
    var result = await _profiling.ProfileActionAsync(async () =>
    {
        // 这里是你要分析的代码
        await SomeExpensiveOperationAsync();
    });

    Console.WriteLine($"执行时间: {result.ExecutionTime}");
    Console.WriteLine($"执行前: {result.BeforeSnapshot.ToFormattedString()}");
    Console.WriteLine($"执行后: {result.AfterSnapshot.ToFormattedString()}");
    
    // 计算内存变化
    var memoryDiff = result.AfterSnapshot.MemoryUsage.WorkingSetMemory - 
                     result.BeforeSnapshot.MemoryUsage.WorkingSetMemory;
    Console.WriteLine($"内存变化: {memoryDiff:+0;-0;0} MB");

    // 分析同步方法
    var syncResult = await _profiling.ProfileAction(() =>
    {
        // 同步代码
        SomeExpensiveOperation();
    });
}
```

### 5. 自定义监控逻辑

```csharp
public class PerformanceMonitor
{
    private readonly IMoProfiling _profiling;
    private readonly ILogger<PerformanceMonitor> _logger;

    public PerformanceMonitor(IMoProfiling profiling, ILogger<PerformanceMonitor> logger)
    {
        _profiling = profiling;
        _logger = logger;
    }

    public async Task MonitorWithAlerts()
    {
        await _profiling.StartMonitoringAsync(TimeSpan.FromSeconds(10), async snapshot =>
        {
            // 记录性能数据
            _logger.LogInformation("性能监测: {Snapshot}", snapshot.ToFormattedString());

            // CPU使用率报警
            if (snapshot.CpuUsage > 75)
            {
                _logger.LogWarning("CPU使用率过高: {CpuUsage}%", snapshot.CpuUsage);
                await HandleHighCpuUsage(snapshot);
            }

            // 内存使用率报警
            if (snapshot.MemoryUsage.MemoryUsagePercentage > 80)
            {
                _logger.LogWarning("内存使用率过高: {MemoryUsage}%", 
                    snapshot.MemoryUsage.MemoryUsagePercentage);
                await HandleHighMemoryUsage(snapshot);
            }

            // 线程数量异常检测
            if (snapshot.ThreadCount > 100)
            {
                _logger.LogWarning("线程数量异常: {ThreadCount}", snapshot.ThreadCount);
                await HandleHighThreadCount(snapshot);
            }
        });
    }

    private async Task HandleHighCpuUsage(PerformanceSnapshot snapshot)
    {
        // 处理高CPU使用率的逻辑
        // 比如：限制并发、发送通知等
    }

    private async Task HandleHighMemoryUsage(PerformanceSnapshot snapshot)
    {
        // 处理高内存使用的逻辑
        // 比如：触发GC、清理缓存等
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private async Task HandleHighThreadCount(PerformanceSnapshot snapshot)
    {
        // 处理线程数量过多的逻辑
        // 比如：检查线程池状态、优化并发逻辑等
    }
}
```

## API 参考

### IMoProfiling 接口

| 方法 | 描述 | 返回类型 |
|------|------|----------|
| `GetCpuUsageAsync()` | 获取CPU使用率（百分比） | `Task<double>` |
| `GetMemoryUsageAsync()` | 获取内存使用情况 | `Task<MemoryUsageInfo>` |
| `GetWorkingSetMemoryAsync()` | 获取工作集内存大小（MB） | `Task<long>` |
| `GetPrivateMemoryAsync()` | 获取私有内存大小（MB） | `Task<long>` |
| `GetThreadCountAsync()` | 获取线程数量 | `Task<int>` |
| `GetHandleCountAsync()` | 获取句柄数量 | `Task<int>` |
| `GetProcessUptimeAsync()` | 获取进程运行时间 | `Task<TimeSpan>` |
| `GetSystemMemoryInfoAsync()` | 获取系统内存信息 | `Task<SystemMemoryInfo>` |
| `GetPerformanceSnapshotAsync()` | 获取性能快照 | `Task<PerformanceSnapshot>` |
| `StartMonitoringAsync()` | 开始连续监控 | `Task` |
| `StopMonitoring()` | 停止监控 | `void` |

### 扩展方法

| 方法 | 描述 | 返回类型 |
|------|------|----------|
| `GetQuickStatusAsync()` | 快速获取基本性能信息 | `Task<string>` |
| `ProfileActionAsync()` | 分析异步方法性能 | `Task<(TimeSpan, PerformanceSnapshot, PerformanceSnapshot)>` |
| `ProfileAction()` | 分析同步方法性能 | `Task<(TimeSpan, PerformanceSnapshot, PerformanceSnapshot)>` |
| `ToFormattedString()` | 格式化性能信息为字符串 | `string` |

## 注意事项

1. **权限要求**: 在某些环境下，访问性能计数器可能需要特定权限
2. **平台兼容性**: 部分功能（如系统内存信息）在Windows平台上功能更完整
3. **性能影响**: 频繁的性能监测本身也会消耗一定资源，建议合理设置监控间隔
4. **资源释放**: 使用完毕后记得调用`StopMonitoring()`或正确处理`IDisposable`

## 最佳实践

1. **合理的监控间隔**: 建议不要低于1秒的监控间隔
2. **异常处理**: 在监控回调中加入适当的异常处理
3. **日志记录**: 结合日志系统记录性能数据和异常情况
4. **报警机制**: 设置合理的阈值和报警逻辑
5. **资源清理**: 在应用程序关闭时正确停止监控并释放资源 