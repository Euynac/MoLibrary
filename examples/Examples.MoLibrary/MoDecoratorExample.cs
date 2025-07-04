using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoDecorator;

namespace Examples.MoLibrary;

/// <summary>
/// MoDecorator 使用示例
/// 展示如何使用 InterfaceProxyDecorationStrategy 来装饰实现特定接口的所有服务
/// </summary>
public static class MoDecoratorExample
{
    /// <summary>
    /// 基础仓储接口
    /// </summary>
    public interface IRepository
    {
        Task<string> GetDataAsync(int id);
    }

    /// <summary>
    /// 用户仓储接口
    /// </summary>
    public interface IUserRepository : IRepository
    {
        Task<string> GetUserNameAsync(int userId);
    }

    /// <summary>
    /// 产品仓储接口
    /// </summary>
    public interface IProductRepository : IRepository
    {
        Task<string> GetProductNameAsync(int productId);
    }

    /// <summary>
    /// 泛型处理器接口
    /// </summary>
    /// <typeparam name="T">处理的数据类型</typeparam>
    public interface IHandler<T>
    {
        Task<T> HandleAsync(T data);
    }

    /// <summary>
    /// 用户仓储实现
    /// </summary>
    public class UserRepository : IUserRepository
    {
        public Task<string> GetDataAsync(int id) => Task.FromResult($"User data: {id}");
        public Task<string> GetUserNameAsync(int userId) => Task.FromResult($"User_{userId}");
    }

    /// <summary>
    /// 产品仓储实现
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        public Task<string> GetDataAsync(int id) => Task.FromResult($"Product data: {id}");
        public Task<string> GetProductNameAsync(int productId) => Task.FromResult($"Product_{productId}");
    }

    /// <summary>
    /// 字符串处理器实现
    /// </summary>
    public class StringHandler : IHandler<string>
    {
        public Task<string> HandleAsync(string data) => Task.FromResult($"Handled: {data}");
    }

    /// <summary>
    /// 整数处理器实现
    /// </summary>
    public class IntHandler : IHandler<int>
    {
        public Task<int> HandleAsync(int data) => Task.FromResult(data * 2);
    }

    /// <summary>
    /// 仓储缓存装饰器
    /// </summary>
    public class CachingRepositoryDecorator : IRepository
    {
        private readonly IRepository _inner;

        public CachingRepositoryDecorator(IRepository inner)
        {
            _inner = inner;
        }

        public async Task<string> GetDataAsync(int id)
        {
            // 简单的缓存逻辑模拟
            Console.WriteLine($"[Cache] Checking cache for repository data: {id}");
            var result = await _inner.GetDataAsync(id);
            Console.WriteLine($"[Cache] Cached repository data: {id} -> {result}");
            return result;
        }
    }

    /// <summary>
    /// 泛型日志装饰器
    /// </summary>
    /// <typeparam name="T">处理的数据类型</typeparam>
    public class LoggingHandler<T> : IHandler<T>
    {
        private readonly IHandler<T> _inner;

        public LoggingHandler(IHandler<T> inner)
        {
            _inner = inner;
        }

        public async Task<T> HandleAsync(T data)
        {
            Console.WriteLine($"[Log] Handling {typeof(T).Name}: {data}");
            var result = await _inner.HandleAsync(data);
            Console.WriteLine($"[Log] Handled {typeof(T).Name}: {data} -> {result}");
            return result;
        }
    }

    /// <summary>
    /// 演示 InterfaceProxyDecorationStrategy 的使用
    /// </summary>
    public static async Task RunExample()
    {
        var services = new ServiceCollection();

        // 注册服务
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IHandler<string>, StringHandler>();
        services.AddScoped<IHandler<int>, IntHandler>();

        // 使用 InterfaceProxyDecorationStrategy 装饰所有实现 IRepository 的服务
        services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();

        // 使用委托装饰所有实现 IHandler<T> 的服务
        services.DecorateInterfaceProxy(typeof(IHandler<>), typeof(LoggingHandler<>));

        // 也可以使用函数式装饰
        services.DecorateInterfaceProxy<IRepository>(repo => 
        {
            return new FunctionalRepositoryDecorator(repo);
        });

        var serviceProvider = services.BuildServiceProvider();

        Console.WriteLine("=== MoDecorator InterfaceProxy Example ===\n");

        // 测试仓储装饰
        Console.WriteLine("1. 测试仓储装饰:");
        var userRepo = serviceProvider.GetRequiredService<IUserRepository>();
        var userData = await userRepo.GetDataAsync(123);
        Console.WriteLine($"Result: {userData}\n");

        var productRepo = serviceProvider.GetRequiredService<IProductRepository>();
        var productData = await productRepo.GetDataAsync(456);
        Console.WriteLine($"Result: {productData}\n");

        // 测试泛型处理器装饰
        Console.WriteLine("2. 测试泛型处理器装饰:");
        var stringHandler = serviceProvider.GetRequiredService<IHandler<string>>();
        var stringResult = await stringHandler.HandleAsync("Hello World");
        Console.WriteLine($"String Result: {stringResult}\n");

        var intHandler = serviceProvider.GetRequiredService<IHandler<int>>();
        var intResult = await intHandler.HandleAsync(42);
        Console.WriteLine($"Int Result: {intResult}\n");

        Console.WriteLine("=== Example Completed ===");
    }

    /// <summary>
    /// 函数式仓储装饰器示例
    /// </summary>
    private class FunctionalRepositoryDecorator : IRepository
    {
        private readonly IRepository _inner;

        public FunctionalRepositoryDecorator(IRepository inner)
        {
            _inner = inner;
        }

        public async Task<string> GetDataAsync(int id)
        {
            Console.WriteLine($"[Functional] Processing repository request: {id}");
            var result = await _inner.GetDataAsync(id);
            Console.WriteLine($"[Functional] Processed repository request: {id}");
            return result;
        }
    }
} 