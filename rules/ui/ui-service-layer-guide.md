# UI服务层开发指南

## 概述
本文档定义了MoLibrary UI模块中服务层的基本规范。

## 1. 服务实现规范

### 1.1 基本结构
```csharp
/// <summary>
/// {ModuleName}服务
/// </summary>
public class {ModuleName}Service
{
    private readonly ILogger<{ModuleName}Service> _logger;
    
    public {ModuleName}Service(ILogger<{ModuleName}Service> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 获取数据
    /// </summary>
    public async Task<Res<TResponse>> GetDataAsync(TRequest request)
    {
        try
        {
            var result = await ProcessDataAsync(request);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取数据失败");
            return Res.Fail($"操作失败: {ex.Message}");
        }
    }
}
```

### 1.2 返回值规范
- 所有公开方法必须返回 `Res<T>` 或 `Res` 类型
- 成功时使用 `Res.Ok(data)` 或 `Res.Ok()`
- 失败时使用 `Res.Fail(message)` 或直接返回错误字符串

## 2. 服务调用

### 2.1 在Blazor组件中使用
```csharp
@inject {ModuleName}Service Service
@inject ISnackbar Snackbar

@code {
    private async Task LoadDataAsync()
    {
        // 使用IsFailed模式
        if ((await Service.GetDataAsync(request)).IsFailed(out var error, out var data))
        {
            Snackbar.Add(error.Message, Severity.Error);
            return;
        }
        
        // 此时data保证不为null
        ProcessData(data);
    }
}
```

### 2.2 服务注册
```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<{ModuleName}Service>();
}
```

## 3. 特殊说明

### SignalRDebugService
SignalRDebugService 因需要保留WebSocket JSON通信调试功能，不遵循标准服务规范。

## 4. 最佳实践
- 使用依赖注入获取所需服务
- 异常必须捕获并返回 `Res.Fail`
- 记录适当的日志信息
- 保持方法的单一职责