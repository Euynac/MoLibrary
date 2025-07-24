# 信息展示中间件使用指南

## 概述

信息展示中间件 (`PipeInfoDisplayMiddlewareBase`) 是 DataChannel 模块的一个新功能，专门用于在 UI 管理界面展示统计信息和业务指标。开发者可以继承此基类来创建自定义的信息统计中间件。

## 特性

- **并发字典存储**：使用 `ConcurrentDictionary<string, object>` 安全地存储统计信息
- **线程安全**：支持多线程环境下的并发访问
- **UI 集成**：信息会自动在 DataChannel 管理界面中以可视化方式展示
- **丰富的辅助方法**：提供计数器、信息设置等便捷方法

## 基础用法

### 1. 创建自定义中间件

```csharp
public class MyCustomMiddleware : PipeInfoDisplayMiddlewareBase
{
    public override DataContext Pass(DataContext context)
    {
        // 统计消息总数
        IncrementCounter("总消息数");
        
        // 设置最后处理时间
        SetInfo("最后处理时间", DateTime.Now);
        
        // 根据业务逻辑进行统计
        if (context.Data?.ToString()?.Contains("error") == true)
        {
            IncrementCounter("错误消息数");
        }
        
        return context;
    }

    public override async Task<DataContext> PassAsync(DataContext context)
    {
        return await Task.FromResult(Pass(context));
    }
}
```

### 2. 注册中间件

```csharp
public class MyChannelBuilder : ISetupPipeline
{
    public void Setup()
    {
        DataPipeline.Create()
            .SetOuterEndpoint(new MetadataForTcp(...))
            .SetInnerEndpoint<MyCustomEndpoint>()
            .AddPipeMiddleware<MyCustomMiddleware>() // 添加信息展示中间件
            .Register("MyChannel", "MyGroup");
    }
}
```

## 内置方法说明

### 信息存储方法

- `SetInfo(string key, object value)`: 设置信息项
- `GetInfo<T>(string key, T defaultValue)`: 获取信息项
- `ClearInfo()`: 清空所有信息

### 计数器方法

- `IncrementCounter(string key, long increment = 1)`: 增加计数器
- `ResetCounter(string key)`: 重置计数器为 0

### 数据访问方法

- `GetInfoDictionary()`: 获取所有信息的只读副本

## 内置示例中间件

### 1. MessageCounterMiddleware

简单的消息计数中间件，统计：
- 消息总数
- 输入/输出消息数
- 错误消息数
- 最后处理时间
- 处理速率

**使用方法**：
```csharp
.AddPipeMiddleware<MessageCounterMiddleware>()
```

### 2. BusinessMessageAnalyzerMiddleware

业务消息分析中间件，提供更详细的统计：
- 消息类型识别（订单、用户、支付等）
- JSON 消息分析
- 消息大小统计
- 处理时间分析
- 平均值计算

**使用方法**：
```csharp
.AddPipeMiddleware<BusinessMessageAnalyzerMiddleware>()
```

## UI 展示效果

在 DataChannel 管理界面中，信息展示中间件会以特殊样式显示：

1. **中间件标识**：显示为"信息展示"类型，使用绿色图标
2. **信息数量提示**：显示字典中信息项的数量
3. **分类展示**：
   - **基础信息**：显示中间件的元数据
   - **统计信息**：以网格形式展示字典中的统计数据
4. **数据格式化**：
   - 数字：添加千位分隔符
   - 时间：格式化为 "yyyy-MM-dd HH:mm:ss"
   - 布尔值：显示为"是/否"

## 最佳实践

### 1. 异常处理

```csharp
public override DataContext Pass(DataContext context)
{
    try
    {
        // 业务逻辑
        IncrementCounter("处理成功数");
        return context;
    }
    catch (Exception ex)
    {
        IncrementCounter("处理异常数");
        SetInfo("最后异常", ex.Message);
        SetInfo("最后异常时间", DateTime.Now);
        return context; // 不影响数据流
    }
}
```

### 2. 性能优化

```csharp
public override DataContext Pass(DataContext context)
{
    // 避免频繁的字符串操作
    IncrementCounter("总数");
    
    // 批量更新，减少字典操作
    var now = DateTime.Now;
    SetInfo("最后更新", now);
    
    return context;
}
```

### 3. 业务指标设计

```csharp
// 使用有意义的键名
IncrementCounter("用户登录次数");
IncrementCounter("支付成功次数");
SetInfo("平均响应时间(毫秒)", averageResponseTime);

// 使用分类前缀
IncrementCounter("业务-订单创建");
IncrementCounter("业务-订单取消");
IncrementCounter("系统-内存使用");
```

## 注意事项

1. **线程安全**：`ConcurrentDictionary` 保证线程安全，但复杂操作需要额外注意
2. **内存使用**：避免在字典中存储大对象，优先使用计数和简单统计
3. **键命名**：使用清晰、有意义的键名，便于 UI 展示
4. **数据类型**：字典支持各种数据类型，UI 会根据类型进行相应的格式化显示

## 扩展开发

如需更复杂的统计功能，可以：

1. **继承并扩展**：在 `PipeInfoDisplayMiddlewareBase` 基础上添加更多辅助方法
2. **组合使用**：在同一个管道中使用多个信息展示中间件
3. **定时更新**：配合定时器实现定期统计信息更新
4. **外部集成**：将统计信息导出到外部监控系统

通过信息展示中间件，您可以轻松实现业务监控和性能分析，提升系统的可观测性。