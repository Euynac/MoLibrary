# MoDecorator - 服务装饰扩展

MoDecorator 是基于 [Scrutor](https://github.com/khellang/Scrutor) 的服务装饰扩展，为 Microsoft.Extensions.DependencyInjection 提供了更强大的装饰功能。

## 特性

### 现有功能（源自 Scrutor）

- **类型装饰** - 装饰指定类型的所有服务
- **泛型装饰** - 支持开放泛型类型的装饰
- **工厂装饰** - 使用委托函数进行装饰
- **条件装饰** - 灵活的装饰条件控制

### 新增功能

#### InterfaceProxyDecorationStrategy

新增的接口代理装饰策略，允许装饰所有实现指定接口的服务，特别适用于：

- **横切关注点** - 日志、缓存、授权、性能监控等
- **批量装饰** - 一次性装饰多个不同类型的服务
- **接口契约** - 基于接口而非具体类型进行装饰

## 使用方法

### 基本用法

```csharp
// 装饰所有实现 IRepository 接口的服务
services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();

// 尝试装饰（不抛出异常）
services.TryDecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
```

### 泛型接口装饰

```csharp
// 装饰所有实现 IHandler<T> 接口的服务
services.DecorateInterfaceProxy(typeof(IHandler<>), typeof(LoggingHandler<>));
```

### 函数式装饰

```csharp
// 使用委托装饰
services.DecorateInterfaceProxy<IService>(service => 
    new LoggingDecorator<IService>(service));

// 带服务提供者的装饰
services.DecorateInterfaceProxy<IService>((service, provider) =>
{
    var logger = provider.GetRequiredService<ILogger<IService>>();
    return new LoggingDecorator<IService>(service, logger);
});
```

### 非泛型 API

```csharp
// 类型安全的装饰
services.DecorateInterfaceProxy(typeof(IRepository), typeof(CachingRepositoryDecorator));

// 函数式装饰
services.DecorateInterfaceProxy(typeof(IRepository), (service, provider) =>
{
    return new CachingRepositoryDecorator((IRepository)service);
});
```

## API 参考

### 扩展方法

| 方法 | 描述 |
|------|------|
| `DecorateInterfaceProxy<TInterface, TDecorator>()` | 使用装饰器类型装饰接口 |
| `TryDecorateInterfaceProxy<TInterface, TDecorator>()` | 尝试装饰接口（不抛出异常） |
| `DecorateInterfaceProxy<TInterface>(Func<TInterface, TInterface>)` | 使用委托装饰接口 |
| `DecorateInterfaceProxy<TInterface>(Func<TInterface, IServiceProvider, TInterface>)` | 使用带服务提供者的委托装饰接口 |
| `DecorateInterfaceProxy(Type, Type)` | 使用类型装饰接口（非泛型） |
| `DecorateInterfaceProxy(Type, Func<object, IServiceProvider, object>)` | 使用委托装饰接口（非泛型） |

### InterfaceProxyDecorationStrategy

核心策略类，支持：

- **接口检测** - 自动检测服务是否实现目标接口
- **泛型处理** - 正确处理开放泛型接口
- **类型安全** - 确保装饰器类型兼容性

## 高级用法

### 多层装饰

```csharp
// 多层装饰会按注册顺序嵌套
services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
services.DecorateInterfaceProxy<IRepository, LoggingRepositoryDecorator>();
services.DecorateInterfaceProxy<IRepository, MetricsRepositoryDecorator>();

// 结果: MetricsRepositoryDecorator -> LoggingRepositoryDecorator -> CachingRepositoryDecorator -> 原始服务
```

### 条件装饰

```csharp
// 结合自定义逻辑进行条件装饰
if (environment.IsDevelopment())
{
    services.DecorateInterfaceProxy<IService, DebuggingServiceDecorator>();
}

if (configuration.GetValue<bool>("EnableCaching"))
{
    services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
}
```

### 与现有装饰组合

```csharp
// 可以与现有的 Decorate 方法组合使用
services.Decorate<ISpecificService, SpecificDecorator>();           // 装饰特定服务
services.DecorateInterfaceProxy<IBaseInterface, CommonDecorator>(); // 装饰接口
```

## 注意事项

1. **装饰顺序** - 后注册的装饰器会包装先注册的装饰器
2. **泛型约束** - 装饰器必须能够接受目标接口的泛型参数
3. **构造函数** - 装饰器必须有一个接受被装饰服务的构造函数
4. **循环依赖** - 避免装饰器与被装饰服务之间的循环依赖

## 最佳实践

### 装饰器设计

```csharp
// 推荐的装饰器模式
public class LoggingRepositoryDecorator : IRepository
{
    private readonly IRepository _inner;
    private readonly ILogger<LoggingRepositoryDecorator> _logger;

    public LoggingRepositoryDecorator(IRepository inner, ILogger<LoggingRepositoryDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(int id)
    {
        _logger.LogInformation("Getting {Type} with id {Id}", typeof(T).Name, id);
        try
        {
            var result = await _inner.GetAsync<T>(id);
            _logger.LogInformation("Successfully got {Type} with id {Id}", typeof(T).Name, id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get {Type} with id {Id}", typeof(T).Name, id);
            throw;
        }
    }
}
```

### 泛型装饰器

```csharp
// 泛型装饰器模式
public class CachingHandler<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    private readonly IMemoryCache _cache;

    public CachingHandler(IHandler<T> inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<TResult> HandleAsync<TResult>(T request)
    {
        var cacheKey = $"{typeof(T).Name}_{typeof(TResult).Name}_{request?.GetHashCode()}";
        
        if (_cache.TryGetValue(cacheKey, out TResult cached))
        {
            return cached;
        }

        var result = await _inner.HandleAsync<TResult>(request);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }
}
```

## 鸣谢

MoDecorator 基于优秀的开源项目 [Scrutor](https://github.com/khellang/Scrutor) 构建。感谢 Scrutor 项目的所有贡献者！

详细鸣谢信息请参见 [ACKNOWLEDGEMENTS.md](./ACKNOWLEDGEMENTS.md)。 