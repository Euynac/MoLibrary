# MoLibrary.DataChannel 框架结构文档

## 1. 框架概述

MoLibrary.DataChannel 是一个灵活、高度可扩展的数据通道框架，用于处理不同系统和组件之间的数据传输和转换。该框架基于管道模式设计，提供了丰富的抽象和扩展点，使开发人员能够快速构建自定义的数据处理流程。

### 1.1 设计原则

- **松耦合**: 通过接口分离和依赖注入实现组件间的松耦合
- **可扩展性**: 提供多个扩展点，支持自定义端点、中间件和转换逻辑
- **双向通信**: 支持内部到外部和外部到内部的双向数据流
- **中间件模式**: 采用中间件链式处理模式，灵活配置数据处理流程
- **统一管理**: 通过中央管理器集中管理和访问所有数据通道

### 1.2 核心功能

- 建立和管理数据通道
- 数据的双向传输和转换
- 通过中间件扩展数据处理逻辑
- 与ASP.NET Core应用程序的集成
- 通道生命周期管理和监控

## 2. 架构组件

### 2.1 核心组件

```
+-----------------------+
|   DataChannelCentral  |
+-----------------------+
           |
           v
+------------------------+      +------------------------+
|     DataChannel        |----->|      DataPipeline     |
+------------------------+      +------------------------+
                                      /            \
                                     /              \
                           +-------------+      +-------------+
                           |InnerEndpoint|      |OuterEndpoint|
                           +-------------+      +-------------+
                                     \              /
                                      \            /
                               +-------------------------+
                               |     Middlewares        |
                               +-------------------------+
```

#### 2.1.1 DataChannelCentral

DataChannelCentral是框架的核心，负责管理和协调所有数据通道:

- 维护所有已注册通道的全局字典
- 提供通道的注册和访问入口
- 管理全局配置和设置
- 处理管道构建和初始化流程

#### 2.1.2 DataChannel

DataChannel封装了管道实例，提供了统一的操作接口:

- 包装底层DataPipeline实例
- 提供通道ID和重新初始化功能
- 作为操作通道的统一入口

#### 2.1.3 DataPipeline

DataPipeline是数据流动和处理的核心组件:

- 连接内部和外部端点
- 维护中间件链
- 处理数据的流动和转换
- 管理管道的生命周期（初始化和释放）

#### 2.1.4 端点 (Endpoints)

端点是管道的两端，负责数据的接收和发送:

- 内部端点 (InnerEndpoint): 处理系统内部数据
- 外部端点 (OuterEndpoint): 与外部系统进行交互
- 提供数据接收和处理的具体实现

#### 2.1.5 中间件 (Middlewares)

中间件负责在数据流经管道的过程中进行处理和转换:

- 端点中间件: 增强端点的功能
- 转换中间件: 处理数据的转换和格式变更
- 监控中间件: 提供对管道的监控和控制

### 2.2 数据模型

#### 2.2.1 DataContext

DataContext是在管道中流动的数据单元:

- 包含原始数据和元数据
- 记录数据来源和操作类型
- 支持不同类型的数据传输（字符串、字节数组、POCO对象）

#### 2.2.2 CommunicationMetadata

通信元数据包含配置通信端点所需的信息:

- 地址、端口等连接信息
- 协议和格式设置
- 端点特定的配置参数

### 2.3 服务与扩展

#### 2.3.1 IDataChannelManager

数据通道管理服务，提供通道的查询和管理功能:

- 按ID获取通道
- 按组获取通道集合
- 列出所有可用通道

#### 2.3.2 DataChannelInitializerService

通道初始化服务，实现IHostedService接口:

- 在应用程序启动时自动初始化所有通道
- 提供通道初始化状态的日志记录
- 支持取消和异常处理

#### 2.3.3 ServiceCollectionExtensions

服务集合扩展方法，用于注册和配置数据通道:

- 添加必要的服务和组件到依赖注入容器
- 配置ASP.NET Core应用程序使用数据通道
- 提供自定义配置选项

## 3. 接口与抽象

### 3.1 核心接口

#### 3.1.1 IPipeComponent

所有管道组件的基础接口，提供元数据访问功能:

```csharp
public interface IPipeComponent
{
    public dynamic GetMetadata();
}
```

#### 3.1.2 IPipeEndpoint

定义管道端点的接口，负责数据的接收和处理:

```csharp
public interface IPipeEndpoint : IWantAccessPipeline, IPipeComponent
{
    public Task ReceiveDataAsync(DataContext data);
    public EDataSource EntranceType { get; internal set; }
}
```

#### 3.1.3 IPipeMiddleware 及派生接口

中间件相关接口，定义数据处理和转换功能:

```csharp
public interface IPipeMiddleware : IPipeComponent { }

public interface IPipeTransformMiddleware : IPipeMiddleware
{
    public Task<DataContext> PassAsync(DataContext context);
}

public interface IPipeEndpointMiddleware : IPipeMiddleware, IWantAccessPipeline { }

public interface IPipeMonitorMiddleware : IPipeTransformMiddleware, IWantAccessPipeline { }
```

### 3.2 功能接口

#### 3.2.1 IWantAccessPipeline

允许组件访问所属管道的接口:

```csharp
public interface IWantAccessPipeline
{
    public DataPipeline Pipe { get; set; }
}
```

#### 3.2.2 IDynamicConfigApplicationBuilder

支持在ASP.NET Core启动时配置应用的接口:

```csharp
public interface IDynamicConfigApplicationBuilder
{
    public void DoConfigApplication(IApplicationBuilder app);
}
```

#### 3.2.3 ISetupPipeline

用于配置和初始化管道的入口接口:

```csharp
public interface ISetupPipeline
{
    void Setup();
}
```

## 4. 使用指南

### 4.1 基本使用流程

1. **注册服务**

```csharp
services.AddDataChannel<MyChannelBuilder>(options => {
    options.EnableControllers = true;
    // 其他配置...
});
```

2. **创建管道构建器**

```csharp
public class MyChannelBuilder : ISetupPipeline
{
    public void Setup()
    {
        // 创建和配置管道
        DataPipeline.Create()
            .SetOuterEndpoint(new MetadataForTcpClient {
                ClientAddress = new KeyValuePair<string, int>("localhost", 8080),
                IsClient = true
            })
            .SetInnerEndpoint<MyCustomEndpoint>()
            .AddPipeMiddleware<LoggingMiddleware>()
            .Register("my-channel-id", "my-channel-group");
    }
}
```

3. **使用数据通道中间件**

```csharp
app.UseDataChannel();
```

4. **访问数据通道**

```csharp
public class MyService
{
    private readonly IDataChannelManager _channelManager;
    
    public MyService(IDataChannelManager channelManager)
    {
        _channelManager = channelManager;
    }
    
    public async Task SendDataAsync(string channelId, object data)
    {
        var channel = _channelManager.Fetch(channelId);
        if (channel != null)
        {
            // 使用通道发送数据
            var context = new DataContext(
                EDataSource.Inner, 
                EDataSource.Inner, 
                EDataOperation.Publish, 
                data
            );
            await channel.Pipe.SendDataAsync(context);
        }
    }
}
```

### 4.2 自定义端点

创建自定义端点以处理特定业务逻辑:

```csharp
public class MyCustomEndpoint : IPipeEndpoint
{
    public DataPipeline Pipe { get; set; }
    
    public EDataSource EntranceType { get; internal set; }
    
    public dynamic GetMetadata() => new ExpandoObject();
    
    public async Task ReceiveDataAsync(DataContext data)
    {
        // 处理接收到的数据
        Console.WriteLine($"Received data: {data.Data}");
        
        // 可能的处理逻辑...
        
        // 如果需要，可以将数据发送到管道的另一端
        // await Pipe.SendDataAsync(newData);
    }
}
```

### 4.3 自定义中间件

创建转换中间件以处理数据转换:

```csharp
public class JsonTransformMiddleware : IPipeTransformMiddleware
{
    public dynamic GetMetadata() => new ExpandoObject();
    
    public async Task<DataContext> PassAsync(DataContext context)
    {
        // 在这里执行数据转换逻辑
        if (context.DataType == EDataType.String && context.Data is string json)
        {
            // 假设将JSON字符串转换为对象
            var obj = JsonSerializer.Deserialize<MyDataObject>(json);
            context.Data = obj;
            context.DataType = EDataType.Poco;
            context.SpecifiedType = typeof(MyDataObject);
        }
        
        return context;
    }
}
```

## 5. 最佳实践

### 5.1 通道设计原则

- **单一职责**: 每个通道应专注于单一的数据传输任务
- **合理分组**: 使用GroupId将相关通道归为一组，便于管理
- **异常处理**: 在端点和中间件中实现适当的异常处理机制
- **资源管理**: 确保通道资源在不再需要时被正确释放

### 5.2 性能优化

- **轻量级数据**: 避免在DataContext中传输大量数据，考虑使用引用或标识符
- **异步处理**: 充分利用异步方法进行IO操作，避免阻塞
- **批处理**: 对于高频数据，考虑实现批处理机制
- **缓存策略**: 适当使用缓存减少重复处理和计算

### 5.3 扩展指南

- **命名规范**: 使用清晰、一致的命名约定
- **文档注释**: 为所有公共接口和关键方法提供详细的文档注释
- **单元测试**: 为自定义组件编写单元测试，确保功能正确性
- **日志集成**: 集成日志框架，记录关键操作和错误信息

## 6. 常见问题

### 6.1 故障排除

- **通道初始化失败**: 检查端点配置和连接参数
- **数据传输错误**: 验证数据格式和DataContext设置
- **资源泄露**: 确保正确实现资源释放逻辑
- **中间件排序**: 注意中间件添加顺序，可能影响处理结果

### 6.2 限制与约束

- 内部和外部端点必须实现IPipeEndpoint接口
- 必须设置外部端点才能注册管道
- 端点和中间件应考虑线程安全问题
- 初始化过程中的异常将标记通道为不可用

## 7. 版本与扩展计划

### 7.1 当前版本特性

- 基础管道框架
- 端点和中间件抽象
- ASP.NET Core集成
- 多通道管理

### 7.2 未来扩展方向

- 更多内置通信协议支持
- 通道监控和统计功能
- 图形化配置界面
- 分布式通道支持
- 更多内置中间件组件

---

## 附录: 核心类型参考

| 类型 | 命名空间 | 描述 |
|------|----------|------|
| `DataChannelCentral` | `MoLibrary.DataChannel` | 数据通道中央管理器 |
| `DataChannel` | `MoLibrary.DataChannel` | 数据通道封装类 |
| `DataChannelSetting` | `MoLibrary.DataChannel` | 数据通道配置类 |
| `DataPipeline` | `MoLibrary.DataChannel.Pipeline` | 数据管道核心类 |
| `DataPipelineBuilder` | `MoLibrary.DataChannel.Pipeline` | 数据管道构建器 |
| `DataContext` | `MoLibrary.DataChannel.Pipeline` | 数据上下文类 |
| `IPipeComponent` | `MoLibrary.DataChannel.Pipeline` | 管道组件基础接口 |
| `IPipeEndpoint` | `MoLibrary.DataChannel.Pipeline` | 管道端点接口 |
| `IPipeMiddleware` | `MoLibrary.DataChannel.Pipeline` | 管道中间件基础接口 |
| `IPipeTransformMiddleware` | `MoLibrary.DataChannel.Pipeline` | 转换中间件接口 |
| `IPipeEndpointMiddleware` | `MoLibrary.DataChannel.Pipeline` | 端点中间件接口 |
| `IPipeMonitorMiddleware` | `MoLibrary.DataChannel.Pipeline` | 监控中间件接口 |
| `IWantAccessPipeline` | `MoLibrary.DataChannel.Interfaces` | 管道访问接口 |
| `IDynamicConfigApplicationBuilder` | `MoLibrary.DataChannel.Interfaces` | 动态应用配置接口 |
| `ISetupPipeline` | `MoLibrary.DataChannel.Interfaces` | 管道设置接口 |
| `IDataChannelManager` | `MoLibrary.DataChannel` | 数据通道管理器接口 |
| `DataChannelManager` | `MoLibrary.DataChannel` | 数据通道管理器实现 |
| `DataChannelInitializerService` | `MoLibrary.DataChannel.Services` | 数据通道初始化服务 |
| `ServiceCollectionExtensions` | `MoLibrary.DataChannel.Extensions` | 服务集合扩展方法 | 