# 内存版取消令牌管理器使用指南

## 概述

`InMemoryCancellationManager` 是一个纯内存版的取消令牌管理器实现，无需轮询，提供即时响应。它适用于单进程/单实例场景，不支持跨进程的分布式取消。

## 主要特点

- **无需轮询**：使用内存事件机制，提供即时响应
- **高性能**：纯内存操作，无网络IO开销
- **简单部署**：不依赖外部存储系统
- **线程安全**：使用ConcurrentDictionary确保线程安全
- **资源管理**：自动管理CancellationTokenSource的生命周期

## 配置方式

### 1. 使用默认配置（内存实现）

```csharp
var builder = WebApplication.CreateBuilder(args);

// 配置内存版取消令牌管理器
builder.ConfigModuleCancellationManager(options =>
{
    options.UseInMemoryImplementation = true;
    options.EnableVerboseLogging = true; // 可选：启用详细日志
});

var app = builder.Build();
```

### 2. 添加键控服务（内存实现）

```csharp
var builder = WebApplication.CreateBuilder(args);

// 添加多个内存版取消令牌管理器实例
builder.ConfigModuleCancellationManager()
    .AddKeyedCancellationManager("instance1", useInMemory: true)
    .AddKeyedCancellationManager("instance2", useInMemory: true);

var app = builder.Build();
```

### 3. 混合使用（部分内存，部分分布式）

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.ConfigModuleCancellationManager()
    .AddKeyedCancellationManager("memory-service", useInMemory: true)
    .AddKeyedCancellationManager("distributed-service", useInMemory: false);

var app = builder.Build();
```

## 使用示例

### 基本用法

```csharp
[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly IMoCancellationManager _cancellationManager;

    public TaskController(IMoCancellationManager cancellationManager)
    {
        _cancellationManager = cancellationManager;
    }

    [HttpPost("start/{taskId}")]
    public async Task<IActionResult> StartTask(string taskId)
    {
        // 获取或创建取消令牌
        var cancellationToken = await _cancellationManager.GetOrCreateTokenAsync(taskId);
        
        // 启动长时间运行的任务
        _ = Task.Run(async () =>
        {
            try
            {
                await LongRunningTask(taskId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Task {taskId} was cancelled");
            }
        });

        return Ok($"Task {taskId} started");
    }

    [HttpPost("cancel/{taskId}")]
    public async Task<IActionResult> CancelTask(string taskId)
    {
        await _cancellationManager.CancelTokenAsync(taskId);
        return Ok($"Task {taskId} cancelled");
    }

    [HttpGet("status/{taskId}")]
    public async Task<IActionResult> GetTaskStatus(string taskId)
    {
        var isCancelled = await _cancellationManager.IsCancelledAsync(taskId);
        return Ok(new { TaskId = taskId, IsCancelled = isCancelled });
    }

    private async Task LongRunningTask(string taskId, CancellationToken cancellationToken)
    {
        for (int i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // 模拟工作
            await Task.Delay(1000, cancellationToken);
            Console.WriteLine($"Task {taskId} - Step {i + 1}/100");
        }
    }
}
```

### 使用键控服务

```csharp
[ApiController]
[Route("api/[controller]")]
public class BatchController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public BatchController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpPost("start-batch/{batchId}")]
    public async Task<IActionResult> StartBatch(string batchId)
    {
        // 使用特定的取消令牌管理器实例
        var cancellationManager = _serviceProvider.GetRequiredKeyedService<IMoCancellationManager>("batch-processor");
        
        var cancellationToken = await cancellationManager.GetOrCreateTokenAsync(batchId);
        
        // 启动批处理任务
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessBatch(batchId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Batch {batchId} was cancelled");
            }
        });

        return Ok($"Batch {batchId} started");
    }

    [HttpPost("cancel-batch/{batchId}")]
    public async Task<IActionResult> CancelBatch(string batchId)
    {
        var cancellationManager = _serviceProvider.GetRequiredKeyedService<IMoCancellationManager>("batch-processor");
        await cancellationManager.CancelTokenAsync(batchId);
        return Ok($"Batch {batchId} cancelled");
    }

    private async Task ProcessBatch(string batchId, CancellationToken cancellationToken)
    {
        // 批处理逻辑
        await Task.Delay(10000, cancellationToken);
    }
}
```

### 批量操作

```csharp
[HttpPost("cancel-multiple")]
public async Task<IActionResult> CancelMultipleTasks([FromBody] List<string> taskIds)
{
    await _cancellationManager.CancelTokensAsync(taskIds);
    return Ok($"Cancelled {taskIds.Count} tasks");
}

[HttpGet("active-tasks")]
public async Task<IActionResult> GetActiveTasks()
{
    var activeKeys = await _cancellationManager.GetActiveTokenKeysAsync();
    return Ok(activeKeys);
}
```

### 令牌重置和清理

```csharp
[HttpPost("reset/{taskId}")]
public async Task<IActionResult> ResetTask(string taskId)
{
    // 重置取消令牌，使其可以重新使用
    await _cancellationManager.ResetTokenAsync(taskId);
    return Ok($"Task {taskId} reset");
}

[HttpDelete("cleanup/{taskId}")]
public async Task<IActionResult> CleanupTask(string taskId)
{
    // 完全删除取消令牌
    await _cancellationManager.DeleteTokenAsync(taskId);
    return Ok($"Task {taskId} cleaned up");
}
```

## 性能对比

| 特性 | 内存版 | 分布式版 |
|------|-------|----------|
| 响应延迟 | 微秒级 | 毫秒级（取决于轮询间隔） |
| 内存占用 | 低 | 中等 |
| 网络IO | 无 | 有 |
| 跨进程支持 | 否 | 是 |
| 部署复杂度 | 低 | 中等 |

## 适用场景

### 适合使用内存版的场景：
- 单体应用
- 本地开发和测试
- 对延迟要求极高的场景
- 不需要跨进程取消的场景
- 简单的任务管理

### 不适合使用内存版的场景：
- 微服务架构
- 需要跨进程取消的场景
- 高可用性要求
- 需要持久化取消状态的场景

## 注意事项

1. **单进程限制**：内存版仅在单个进程内有效，重启应用会丢失所有状态
2. **内存占用**：长期运行的应用需要注意及时清理不用的令牌
3. **并发安全**：虽然使用了线程安全的数据结构，但在高并发场景下仍需注意性能
4. **日志记录**：建议在生产环境中启用适当级别的日志记录

## 最佳实践

1. **及时清理**：完成的任务应调用 `DeleteTokenAsync` 清理资源
2. **异常处理**：始终正确处理 `OperationCanceledException`
3. **状态检查**：在长时间运行的循环中定期检查取消状态
4. **键命名**：使用有意义且唯一的键名，避免冲突 