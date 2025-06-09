# ProgressBar 模块使用指南

## 概述

ProgressBar 模块提供了一个完整的进度条管理系统，支持状态存储、取消操作和事件通知。

## 功能特性

- 进度状态持久化存储（支持内存和分布式存储）
- 分布式取消令牌支持
- 自动更新机制
- 事件通知（状态更新、完成、取消）
- 预估剩余时间计算
- 后台清理过期任务

## 模块注册

```csharp
// 在 Program.cs 中注册模块
builder.ConfigModuleProgressBar(options =>
{
    options.UseDistributedStateStore = true; // 使用分布式存储
})
.Register()
.RegisterProgressBarService(); // 注册进度条服务
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
// 更新到指定步数
await progressBar.UpdateStatusAsync(50, "处理中...");

// 递增进度
await progressBar.IncrementAsync(1, "完成一个项目");

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
            
            // 更新进度
            await IncrementAsync(1, $"处理第 {i + 1} 项");
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

## 配置选项

```csharp
builder.ConfigModuleProgressBar(options =>
{
    options.UseDistributedStateStore = true; // 使用分布式存储
})
.Register()
.RegisterProgressBarService()
.AddKeyedProgressBarService("special-tasks"); // 注册特定键的服务
```

## 依赖模块

- **StateStore**: 用于状态持久化
- **CancellationManager**: 用于分布式取消令牌管理

这些依赖模块会自动注册和配置。 