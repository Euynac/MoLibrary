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

// 递增进度
await progressBar.IncrementAsync(1, "完成一个项目");

// 强制立即保存状态
await progressBar.SaveStatus(saveInstantly: true);

// 检查是否被取消
progressBar.ThrowIfCancellationRequested();
```

### 3. 完成或取消任务

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

### 自定义进度条类

```csharp
public class CustomProgressBar : ProgressBar
{
    public CustomProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
        : base(setting, service, taskId)
    {
    }

    // 自定义逻辑
    public async Task ProcessWithCustomLogic()
    {
        for (int i = 0; i < Status.TotalSteps; i++)
        {
            ThrowIfCancellationRequested(); // 检查取消
            
            // 执行业务逻辑
            await DoSomeWork();
            
            // 更新进度（自动优化保存）
            await IncrementAsync(1, $"处理第 {i + 1} 项");
            
            // 每10项强制保存一次
            if (i % 10 == 0)
            {
                await SaveStatus(saveInstantly: true);
            }
        }
        
        await CompleteTaskAsync();
    }
}

// 使用自定义进度条
var customProgressBar = await _progressBarService.CreateProgressBarAsync<CustomProgressBar>("custom-task");
```

### 分布式取消

```csharp
// 在一个服务中创建进度条
var progressBar = await _progressBarService.CreateProgressBarAsync("distributed-task");

// 在另一个服务中取消任务
await _progressBarService.CancelProgressBarAsync(progressBar, "管理员取消");

// 进度条会自动检测到取消状态
progressBar.ThrowIfCancellationRequested(); // 会抛出异常
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