# MoAmbientData 模块

MoAmbientData 模块提供了一个在 Scoped 生命周期内临时存储和管理状态数据的解决方案。该模块特别适用于在请求处理过程中需要跨多个服务或组件共享上下文信息的场景。

## 功能特点

- **Scoped 生命周期**: 数据在 Scoped 容器生命周期内有效，自动销毁
- **线程安全**: 使用 ConcurrentDictionary 确保线程安全
- **类型安全**: 支持泛型方法进行类型转换
- **扩展方法**: 提供常用场景的便捷扩展方法
- **链式调用**: 支持链式操作提高代码可读性

## 安装和配置

### 1. 注册模块

```csharp
var builder = WebApplication.CreateBuilder(args);

// 注册 AmbientData 模块
builder.ConfigModuleAmbientData();

var app = builder.Build();
```

### 2. 依赖注入使用

```csharp
public class YourService
{
    private readonly IMoAmbientData _ambientData;

    public YourService(IMoAmbientData ambientData)
    {
        _ambientData = ambientData;
    }

    public void DoSomething()
    {
        // 设置数据
        _ambientData.SetData("key", "value");
        
        // 获取数据
        var value = _ambientData.GetData<string>("key");
    }
}
```

## 基本用法

### 设置和获取数据

```csharp
// 设置数据
_ambientData.SetData("userId", "12345");
_ambientData.SetData("requestTime", DateTime.Now);
_ambientData.SetData("isAdmin", true);

// 获取数据
var userId = _ambientData.GetData<string>("userId");
var requestTime = _ambientData.GetData<DateTime>("requestTime");
var isAdmin = _ambientData.GetData<bool>("isAdmin");

// 获取数据并提供默认值
var timeout = _ambientData.GetData("timeout", 30);
```

### 使用索引器

```csharp
// 设置数据
_ambientData["userId"] = "12345";
_ambientData["requestTime"] = DateTime.Now;

// 获取数据
var userId = _ambientData["userId"] as string;
var requestTime = _ambientData["requestTime"] as DateTime?;
```

### 数据管理

```csharp
// 检查数据是否存在
if (_ambientData.HasData("userId"))
{
    // 数据存在
}

// 移除数据
_ambientData.RemoveData("userId");

// 清空所有数据
_ambientData.Clear();
```

## 扩展方法

模块提供了常用场景的扩展方法：

```csharp
// 设置常用数据（支持链式调用）
_ambientData
    .SetUserId("12345")
    .SetTenantId("tenant-001")
    .SetRequestId(Guid.NewGuid().ToString())
    .SetCurrentTimestamp();

// 获取常用数据
var userId = _ambientData.GetUserId();
var tenantId = _ambientData.GetTenantId();
var requestId = _ambientData.GetRequestId();
var timestamp = _ambientData.GetTimestamp();
```

### 可用的扩展方法

- `SetUserId()` / `GetUserId()` - 用户ID
- `SetTenantId()` / `GetTenantId()` - 租户ID
- `SetRequestId()` / `GetRequestId()` - 请求ID
- `SetTraceId()` / `GetTraceId()` - 跟踪ID
- `SetOperationType()` / `GetOperationType()` - 操作类型
- `SetTimestamp()` / `GetTimestamp()` - 时间戳
- `SetCurrentTimestamp()` - 设置当前时间戳

## 使用场景

### 1. Web API 请求上下文

```csharp
// 在中间件中设置请求上下文
public class RequestContextMiddleware
{
    public async Task InvokeAsync(HttpContext context, IMoAmbientData ambientData)
    {
        ambientData
            .SetRequestId(context.TraceIdentifier)
            .SetUserId(context.User.Identity?.Name ?? "Anonymous")
            .SetCurrentTimestamp();

        await _next(context);
    }
}

// 在服务中使用上下文信息
public class BusinessService
{
    public void ProcessData(IMoAmbientData ambientData)
    {
        var requestId = ambientData.GetRequestId();
        var userId = ambientData.GetUserId();
        
        // 使用上下文信息进行业务处理
        _logger.LogInformation("Processing data for user {UserId} in request {RequestId}", 
            userId, requestId);
    }
}
```

### 2. 审计日志

```csharp
public class AuditService
{
    public void LogOperation(string operation, IMoAmbientData ambientData)
    {
        var auditLog = new AuditLog
        {
            UserId = ambientData.GetUserId(),
            Operation = operation,
            Timestamp = ambientData.GetTimestamp() ?? DateTime.UtcNow,
            RequestId = ambientData.GetRequestId()
        };
        
        // 保存审计日志
    }
}
```

### 3. 多租户支持

```csharp
public class TenantService
{
    public void ProcessTenantData(IMoAmbientData ambientData)
    {
        var tenantId = ambientData.GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException("Tenant context not found");
        }
        
        // 基于租户ID处理数据
    }
}
```

## 与类似技术的比较

### HttpContext.Items
- **优点**: ASP.NET Core 内置，HTTP 请求生命周期
- **缺点**: 仅限于 HTTP 请求，不适用于后台任务

### AsyncLocal<T>
- **优点**: 异步调用链中保持状态
- **缺点**: 需要静态定义，不支持动态键值对

### MoAmbientData
- **优点**: 
  - 通用的 Scoped 生命周期
  - 动态键值对存储
  - 类型安全的访问
  - 丰富的扩展方法
  - 支持依赖注入

## 注意事项

1. **生命周期**: 数据仅在 Scoped 生命周期内有效
2. **线程安全**: 模块本身是线程安全的，但不保证跨 Scope 的数据一致性
3. **性能**: 基于 ConcurrentDictionary，适合频繁读写操作
4. **内存**: 数据会在 Scope 结束时自动清理

## 最佳实践

1. **使用常量定义键名**: 避免硬编码键名，使用 `MoAmbientDataExtensions.Keys` 或自定义常量
2. **及时清理**: 对于大数据对象，考虑及时调用 `RemoveData()`
3. **类型安全**: 优先使用泛型方法而不是索引器
4. **扩展方法**: 为常用场景创建专门的扩展方法
5. **异常处理**: 在获取关键数据时进行空值检查 