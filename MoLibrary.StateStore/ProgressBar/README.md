# ProgressBar 模块使用指南

## 概述

ProgressBar 模块提供了一个完整的进度条管理系统，支持状态存储、取消操作和事件通知。

## 功能特性

- 进度状态持久化存储（支持内存和分布式存储）
- 分布式取消令牌支持
- 智能自动更新机制（避免频繁保存）
- 事件通知（状态更新、完成、取消）
- 预估剩余时间计算
- 统一异常处理机制
- 自动清理机制（自动更新任务自清理）

## 模块注册

```csharp
// 在 Program.cs 中注册模块
builder.ConfigModuleProgressBar(options =>
{
    options.UseDistributedStateStore = true; // 使用分布式存储
})
.Register(); // 自动注册进度条服务
```

## 基本使用

### 1. 创建进度条

```csharp
// 注入服务
private readonly IMoProgressBarService _progressBarService;

// 创建进度条
var progressBar = await _progressBarService.CreateProgressBarAsync("my-task", setting =>
{
    setting.TotalSteps = 100;
    setting.AutoUpdateDuration = TimeSpan.FromSeconds(5); // 每5秒自动保存状态
    setting.TimeToLive = TimeSpan.FromMinutes(30); // 30分钟后过期
});
```

### 2. 更新进度

```csharp
// 更新到指定步数（如果设置了自动更新，不会立即保存）
await progressBar.UpdateStatusAsync(50, "处理中...");

// 更新进度并设置阶段
await progressBar.UpdateStatusAsync(50, "处理中...", "数据处理阶段");

// 递增进度
await progressBar.IncrementAsync(1, "完成一个项目");

// 递增进度并更新阶段
await progressBar.IncrementAsync(1, "完成一个项目", "文件处理阶段");

// 只更新阶段
await progressBar.UpdatePhaseAsync("初始化阶段", "准备开始处理...");

// 强制立即保存状态
await progressBar.SaveStatus(saveInstantly: true);

// 检查是否被取消
progressBar.ThrowIfCancellationRequested();
```

### 3. 获取进度状态

```csharp
// 获取基本状态
var status = await _progressBarService.GetProgressBarStatus("my-task");
Console.WriteLine($"进度: {status.Percentage}%");
Console.WriteLine($"当前阶段: {status.Phase}");
Console.WriteLine($"当前状态: {status.CurrentStatus}");
Console.WriteLine($"是否已取消: {status.IsCancelled}");
Console.WriteLine($"取消原因: {status.CancelReason}");

// 获取自定义状态
var customStatus = await _progressBarService.GetProgressBarStatus<CustomProgressBarStatus>("custom-task");
if (customStatus != null)
{
    Console.WriteLine($"自定义属性: {customStatus.CustomProperty}");
}

// 检查任务状态
if (status.IsCancelled)
{
    Console.WriteLine($"任务已被取消，原因: {status.CancelReason}");
}
else if (status.Percentage >= 100)
{
    Console.WriteLine("任务已完成");
}
else
{
    Console.WriteLine($"任务进行中: {status.Percentage}%");
}
```

### 4. 完成或取消任务

```csharp
// 完成任务
await progressBar.CompleteTaskAsync();

// 取消任务
await progressBar.CancelTaskAsync("用户取消");
```

## 自动更新机制

当设置了 `AutoUpdateDuration` 时：

- 普通的 `UpdateStatusAsync()` 调用不会立即保存到存储
- 系统会按设定间隔自动保存状态
- 需要立即保存时，使用 `SaveStatus(saveInstantly: true)`
- 任务完成或取消时会自动停止自动更新

```csharp
var progressBar = await _progressBarService.CreateProgressBarAsync("auto-task", setting =>
{
    setting.AutoUpdateDuration = TimeSpan.FromSeconds(10); // 每10秒自动保存
});

// 这些调用不会立即保存（因为有自动更新）
await progressBar.UpdateStatusAsync(10);
await progressBar.UpdateStatusAsync(20);

// 强制立即保存
await progressBar.SaveStatus(saveInstantly: true);
```

## 事件监听

```csharp
// 监听状态更新
progressBar.StatusUpdated += (sender, e) =>
{
    Console.WriteLine($"进度: {e.Status.Status.Percentage}%");
};

// 监听取消事件
progressBar.Cancelled += (sender, e) =>
{
    Console.WriteLine($"任务被取消: {e.Reason}");
};

// 监听完成事件
progressBar.Completed += (sender, e) =>
{
    Console.WriteLine("任务完成!");
};
```

## 异常处理

模块使用统一的异常处理机制，所有错误都会通过 `CreateException()` 方法包装，提供详细的错误信息和日志记录。

```csharp
try
{
    var progressBar = await _progressBarService.CreateProgressBarAsync("test");
    await progressBar.UpdateStatusAsync(50);
}
catch (Exception ex)
{
    // 异常会包含详细的上下文信息
    _logger.LogError(ex, "进度条操作失败");
}
```

## 高级用法

### 自定义进度条状态

```csharp
// 定义自定义状态类
public class FileProcessingStatus : ProgressBarStatus
{
    public FileProcessingStatus(int totalSteps) : base(totalSteps)
    {
    }

    public int ProcessedFiles { get; set; }
    public long ProcessedBytes { get; set; }
    public string? CurrentFileName { get; set; }
}

// 定义自定义进度条类
public class FileProcessingProgressBar : ProgressBar
{
    public FileProcessingProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
        : base(setting, service, taskId)
    {
        // 使用自定义状态替换默认状态
        Status = new FileProcessingStatus(setting.TotalSteps);
    }

    public new FileProcessingStatus Status { get; private set; }

    public async Task ProcessFileAsync(string fileName, long fileSize)
    {
        Status.ProcessedFiles++;
        Status.ProcessedBytes += fileSize;
        Status.CurrentFileName = fileName;

        await IncrementAsync(1, $"处理文件: {fileName}", "文件处理");
    }
}

// 使用自定义进度条
var fileProgressBar = await _progressBarService.CreateProgressBarAsync<FileProcessingProgressBar>("file-task");
await fileProgressBar.ProcessFileAsync("document.pdf", 1024000);
```

### 阶段化进度跟踪

```csharp
var progressBar = await _progressBarService.CreateProgressBarAsync("staged-task", setting =>
{
    setting.TotalSteps = 100;
});

// 第一阶段：初始化
await progressBar.UpdatePhaseAsync("初始化", "正在初始化系统...");
for (int i = 1; i <= 20; i++)
{
    await progressBar.UpdateStatusAsync(i, $"初始化步骤 {i}/20");
    await Task.Delay(100);
}

// 第二阶段：数据处理
await progressBar.UpdatePhaseAsync("数据处理", "正在处理业务数据...");
for (int i = 21; i <= 80; i++)
{
    await progressBar.UpdateStatusAsync(i, $"处理数据 {i-20}/60");
    await Task.Delay(50);
}

// 第三阶段：完成
await progressBar.UpdatePhaseAsync("完成", "正在保存结果...");
for (int i = 81; i <= 100; i++)
{
    await progressBar.UpdateStatusAsync(i, $"保存结果 {i-80}/20");
    await Task.Delay(30);
}

await progressBar.CompleteTaskAsync();
```

### 取消原因跟踪

```csharp
var progressBar = await _progressBarService.CreateProgressBarAsync("cancel-test");

// 监听取消事件
progressBar.Cancelled += (sender, e) =>
{
    Console.WriteLine($"任务被取消: {e.Reason}");
    // 状态中也会保存取消原因和取消标记
    Console.WriteLine($"状态中的取消原因: {e.Status.Status.CancelReason}");
    Console.WriteLine($"状态中的取消标记: {e.Status.Status.IsCancelled}");
};

try
{
    await progressBar.UpdatePhaseAsync("处理中", "执行重要任务...");
    // ... 处理逻辑
}
catch (Exception ex)
{
    await progressBar.CancelTaskAsync($"发生错误: {ex.Message}");
}

// 后续可以通过状态获取取消信息
var status = await _progressBarService.GetProgressBarStatus("cancel-test");
if (status.IsCancelled)
{
    Console.WriteLine($"任务已取消，原因: {status.CancelReason}");
}

// 其他微服务实例也可以通过状态判断取消状态
// 在微服务B中获取状态
var taskStatus = await _progressBarService.GetProgressBarStatus("cancel-test");
if (taskStatus.IsCancelled)
{
    // 处理任务已被取消的情况
    Console.WriteLine("检测到任务已被其他服务取消，停止相关处理");
    return;
}
```

### 获取自定义状态

```csharp
// 获取基本状态
var basicStatus = await _progressBarService.GetProgressBarStatus("my-task");

// 获取自定义状态
var customStatus = await _progressBarService.GetProgressBarStatus<FileProcessingStatus>("file-task");
if (customStatus != null)
{
    Console.WriteLine($"已处理文件: {customStatus.ProcessedFiles}");
    Console.WriteLine($"已处理字节: {customStatus.ProcessedBytes}");
    Console.WriteLine($"当前文件: {customStatus.CurrentFileName}");
    Console.WriteLine($"当前阶段: {customStatus.Phase}");
    Console.WriteLine($"进度: {customStatus.Percentage}%");
}
```

## 状态属性说明

### ProgressBarStatus 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `TotalSteps` | `int` | 总步数 |
| `CurrentStep` | `int` | 当前步数 |
| `CurrentStatus` | `string?` | 当前状态描述（细致的进度描述） |
| `Phase` | `string?` | 当前阶段（粗粒度的进度阶段） |
| `IsCancelled` | `bool` | 是否已取消 |
| `CancelReason` | `string?` | 取消原因 |
| `StartTime` | `DateTime` | 任务开始时间 |
| `LastUpdated` | `DateTime` | 最后更新时间 |
| `ElapsedTime` | `TimeSpan` | 已经过的时间 |
| `Percentage` | `double` | 进度百分比 |
| `EstimatedRemaining` | `TimeSpan` | 预估剩余时间 |

### Phase vs CurrentStatus

- **Phase**: 表示当前任务的大的执行阶段，如"初始化"、"数据处理"、"完成"等
- **CurrentStatus**: 表示当前具体的操作状态，如"正在处理第5个文件"、"保存到数据库"等

例如：
```csharp
Phase: "数据处理阶段"
CurrentStatus: "正在处理用户数据 (1250/5000)"
```

## 最佳实践

### 1. 合理使用阶段划分

```csharp
// 好的做法：清晰的阶段划分
await progressBar.UpdatePhaseAsync("数据验证", "验证输入数据格式...");
await progressBar.UpdatePhaseAsync("数据转换", "转换数据格式...");
await progressBar.UpdatePhaseAsync("数据保存", "保存到数据库...");

// 避免：过于细致的阶段划分
// await progressBar.UpdatePhaseAsync("验证第1个字段", "...");
// await progressBar.UpdatePhaseAsync("验证第2个字段", "...");
```

### 2. 有意义的取消原因

```csharp
// 好的做法：具体的取消原因
await progressBar.CancelTaskAsync("用户手动取消操作");
await progressBar.CancelTaskAsync("网络连接超时");
await progressBar.CancelTaskAsync("磁盘空间不足");

// 避免：模糊的取消原因
// await progressBar.CancelTaskAsync("出错了");
```

### 3. 自定义状态的合理设计

```csharp
// 好的做法：有业务意义的自定义属性
public class DataMigrationStatus : ProgressBarStatus
{
    public int MigratedRecords { get; set; }
    public int FailedRecords { get; set; }
    public string? CurrentTable { get; set; }
    public List<string> Errors { get; set; } = new();
}

// 避免：过多的自定义属性影响性能
```

## 性能优化特性

1. **智能保存**: 设置自动更新后，频繁的进度更新不会立即保存
2. **自动清理**: 完成或取消的任务会自动停止后台更新
3. **TTL支持**: 过期任务自动从存储中清理
4. **异常优化**: 统一的异常处理减少重复代码

## 配置选项

```csharp
builder.ConfigModuleProgressBar(options =>
{
    options.UseDistributedStateStore = true; // 使用分布式存储
})
.Register(); // 自动注册服务，无需手动调用RegisterProgressBarService()
```

## 依赖模块

- **StateStore**: 用于状态持久化
- **CancellationManager**: 用于分布式取消令牌管理

这些依赖模块会自动注册和配置。

## 跨微服务状态检查

`IsCancelled` 属性的主要价值在于支持跨微服务的状态同步。当一个微服务取消了任务，其他微服务可以通过检查状态来及时发现并作出相应处理。

### 场景示例

```csharp
// 微服务A - 数据处理服务
public class DataProcessingService
{
    public async Task ProcessDataAsync(string taskId)
    {
        var progressBar = await _progressBarService.CreateProgressBarAsync(taskId);
        
        try
        {
            await progressBar.UpdatePhaseAsync("数据验证", "开始验证数据...");
            // 处理逻辑...
            
            if (someErrorCondition)
            {
                await progressBar.CancelTaskAsync("数据验证失败");
                return;
            }
        }
        catch (Exception ex)
        {
            await progressBar.CancelTaskAsync($"处理异常: {ex.Message}");
            throw;
        }
    }
}

// 微服务B - 通知服务
public class NotificationService
{
    public async Task MonitorTaskAsync(string taskId)
    {
        while (true)
        {
            var status = await _progressBarService.GetProgressBarStatus(taskId);
            
            if (status.IsCancelled)
            {
                // 发送取消通知
                await SendCancellationNotification(taskId, status.CancelReason);
                break;
            }
            
            if (status.Percentage >= 100)
            {
                // 发送完成通知
                await SendCompletionNotification(taskId);
                break;
            }
            
            // 发送进度通知
            await SendProgressNotification(taskId, status.Percentage, status.Phase);
            
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}

// 微服务C - 文件清理服务
public class FileCleanupService
{
    public async Task CleanupTempFilesAsync(string taskId)
    {
        await Task.Delay(TimeSpan.FromMinutes(5)); // 等待一段时间再清理
        
        var status = await _progressBarService.GetProgressBarStatus(taskId);
        
        if (status.IsCancelled)
        {
            // 如果任务被取消，立即清理临时文件
            await CleanupFiles(taskId);
            _logger.LogInformation("任务 {TaskId} 被取消，已清理临时文件。原因: {Reason}", 
                taskId, status.CancelReason);
        }
        else if (status.Percentage >= 100)
        {
            // 如果任务完成，正常清理
            await CleanupFiles(taskId);
            _logger.LogInformation("任务 {TaskId} 已完成，已清理临时文件", taskId);
        }
    }
}
```

### 状态检查最佳实践

```csharp
// 1. 定期检查任务状态
public async Task MonitorTaskStatus(string taskId, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var status = await _progressBarService.GetProgressBarStatus(taskId);
            
            // 检查取消状态
            if (status.IsCancelled)
            {
                _logger.LogWarning("任务 {TaskId} 已被取消: {Reason}", taskId, status.CancelReason);
                await HandleTaskCancellation(taskId, status.CancelReason);
                break;
            }
            
            // 检查完成状态
            if (status.Percentage >= 100)
            {
                _logger.LogInformation("任务 {TaskId} 已完成", taskId);
                await HandleTaskCompletion(taskId);
                break;
            }
            
            // 处理进度更新
            await HandleProgressUpdate(taskId, status);
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查任务状态时出错: {TaskId}", taskId);
        }
        
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
    }
}

// 2. 在关键操作前检查状态
public async Task ProcessBatchData(string taskId, IEnumerable<DataItem> items)
{
    foreach (var item in items)
    {
        // 在处理每个批次前检查取消状态
        var status = await _progressBarService.GetProgressBarStatus(taskId);
        if (status.IsCancelled)
        {
            _logger.LogInformation("检测到任务已取消，停止批次处理");
            return;
        }
        
        await ProcessItem(item);
    }
}
``` 