using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoDecorator;
using Xunit;

namespace Scrutor.Tests;

/// <summary>
/// Tests for InterfaceProxyDecorationStrategy functionality
/// </summary>
public class InterfaceProxyDecorationTests : TestBase
{
    #region Basic Interface Proxy Decoration Tests

    [Fact]
    public void CanDecorateInterfaceProxy_BasicInterface()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IProductRepository, ProductRepository>();

            services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();
        var productRepo = provider.GetRequiredService<IProductRepository>();

        var userDecorator = Assert.IsType<CachingRepositoryDecorator>(userRepo);
        var productDecorator = Assert.IsType<CachingRepositoryDecorator>(productRepo);

        Assert.IsType<UserRepository>(userDecorator.Inner);
        Assert.IsType<ProductRepository>(productDecorator.Inner);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_NonGenericAPI()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();

            services.DecorateInterfaceProxy(typeof(IRepository), typeof(CachingRepositoryDecorator));
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();
        Assert.IsType<CachingRepositoryDecorator>(userRepo);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_WithFunction()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();

            services.DecorateInterfaceProxy<IRepository>(repo => new LoggingRepositoryDecorator(repo));
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();

        var decorator = Assert.IsType<LoggingRepositoryDecorator>(userRepo);
        Assert.IsType<UserRepository>(decorator.Inner);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_WithServiceProviderFunction()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<ICacheService, CacheService>();
            services.AddSingleton<IUserRepository, UserRepository>();

            services.DecorateInterfaceProxy<IRepository>((repo, provider) =>
            {
                var cache = provider.GetRequiredService<ICacheService>();
                return new ServiceDependentRepositoryDecorator(repo, cache);
            });
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();

        var decorator = Assert.IsType<ServiceDependentRepositoryDecorator>(userRepo);
        Assert.IsType<UserRepository>(decorator.Inner);
        Assert.NotNull(decorator.CacheService);
    }

    #endregion

    #region Generic Interface Decoration Tests

    [Fact]
    public void CanDecorateInterfaceProxy_GenericInterface()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IHandler<string>, StringHandler>();
            services.AddSingleton<IHandler<int>, IntHandler>();

            services.DecorateInterfaceProxy(typeof(IHandler<>), typeof(LoggingHandler<>));
        });

        var stringHandler = provider.GetRequiredService<IHandler<string>>();
        var intHandler = provider.GetRequiredService<IHandler<int>>();

        var stringDecorator = Assert.IsType<LoggingHandler<string>>(stringHandler);
        var intDecorator = Assert.IsType<LoggingHandler<int>>(intHandler);

        Assert.IsType<StringHandler>(stringDecorator.Inner);
        Assert.IsType<IntHandler>(intDecorator.Inner);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_GenericInterface_WithFunction()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IHandler<string>, StringHandler>();

            services.DecorateInterfaceProxy(typeof(IHandler<>), (handler, provider) =>
            {
                var handlerType = handler.GetType();
                var handlerInterface = handlerType.GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>));
                var dataType = handlerInterface.GetGenericArguments()[0];
                var decoratorType = typeof(LoggingHandler<>).MakeGenericType(dataType);
                return Activator.CreateInstance(decoratorType, handler);
            });
        });

        var stringHandler = provider.GetRequiredService<IHandler<string>>();

        var decorator = Assert.IsType<LoggingHandler<string>>(stringHandler);
        Assert.IsType<StringHandler>(decorator.Inner);
    }

    #endregion

    #region Multiple Services and Multi-layer Decoration Tests

    [Fact]
    public void CanDecorateInterfaceProxy_MultipleServices()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IProductRepository, ProductRepository>();

            services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
        });

        var repositories = provider.GetRequiredService<IEnumerable<IRepository>>().ToArray();

        Assert.Equal(2, repositories.Length);
        Assert.All(repositories, repo => Assert.IsType<CachingRepositoryDecorator>(repo));
    }

    [Fact]
    public void CanDecorateInterfaceProxy_MultiLayerDecoration()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();

            services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
            services.DecorateInterfaceProxy<IRepository, LoggingRepositoryDecorator>();
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();

        var outerDecorator = Assert.IsType<LoggingRepositoryDecorator>(userRepo);
        var innerDecorator = Assert.IsType<CachingRepositoryDecorator>(outerDecorator.Inner);
        var original = Assert.IsType<UserRepository>(innerDecorator.Inner);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_CombinedWithRegularDecoration()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IProductRepository, ProductRepository>();

            // First decorate all IRepository implementations
            services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
            
            // Then decorate specific IUserRepository
            services.Decorate<IUserRepository, SpecificUserRepositoryDecorator>();
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();
        var productRepo = provider.GetRequiredService<IProductRepository>();

        // UserRepository should have both decorators
        var specificDecorator = Assert.IsType<SpecificUserRepositoryDecorator>(userRepo);
        var cacheDecorator = Assert.IsType<CachingRepositoryDecorator>(specificDecorator.Inner);
        Assert.IsType<UserRepository>(cacheDecorator.Inner);

        // ProductRepository should only have the cache decorator
        var productCacheDecorator = Assert.IsType<CachingRepositoryDecorator>(productRepo);
        Assert.IsType<ProductRepository>(productCacheDecorator.Inner);
    }

    #endregion

    #region Error Handling and Edge Cases

    [Fact]
    public void DecorateInterfaceProxy_ThrowsWhenNoMatchingService()
    {
        var exception = Assert.Throws<DecorationException>(() =>
        {
            ConfigureProvider(services =>
            {
                services.AddSingleton<IUnrelatedService, UnrelatedService>();

                services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
            });
        });

        Assert.Contains("IRepository", exception.Message);
    }

    [Fact]
    public void TryDecorateInterfaceProxy_ReturnsFalseWhenNoMatchingService()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUnrelatedService, UnrelatedService>();

            var result = services.TryDecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
            Assert.False(result);
        });

        var unrelatedService = provider.GetRequiredService<IUnrelatedService>();
        Assert.IsType<UnrelatedService>(unrelatedService);
    }

    [Fact]
    public void TryDecorateInterfaceProxy_ReturnsTrueWhenMatchingService()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();

            var result = services.TryDecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
            Assert.True(result);
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();
        Assert.IsType<CachingRepositoryDecorator>(userRepo);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_WithServiceProvider()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<ICacheService, CacheService>();
            services.AddSingleton<IUserRepository, UserRepository>();

            services.DecorateInterfaceProxy<IRepository>((repo, provider) =>
            {
                var cache = provider.GetRequiredService<ICacheService>();
                return new ServiceDependentRepositoryDecorator(repo, cache);
            });
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();

        var decorator = Assert.IsType<ServiceDependentRepositoryDecorator>(userRepo);
        Assert.IsType<UserRepository>(decorator.Inner);
        Assert.NotNull(decorator.CacheService);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_ServiceKeyGeneration()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IUserRepository, UserRepository>();
        services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();

        var descriptors = services.GetDescriptors<IUserRepository>();

        Assert.Equal(2, descriptors.Length);

        var decoratedDescriptor = descriptors.SingleOrDefault(x => x.ServiceKey is not null);
        Assert.NotNull(decoratedDescriptor);

        var key = Assert.IsType<string>(decoratedDescriptor.ServiceKey);
        Assert.StartsWith("IUserRepository", key);
        Assert.EndsWith("+Decorated", key);
    }

    [Fact]
    public void CanDecorateInterfaceProxy_SelectiveDecoration()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IUnrelatedService, UnrelatedService>();

            services.DecorateInterfaceProxy<IRepository, CachingRepositoryDecorator>();
        });

        var userRepo = provider.GetRequiredService<IUserRepository>();
        var unrelatedService = provider.GetRequiredService<IUnrelatedService>();

        Assert.IsType<CachingRepositoryDecorator>(userRepo);
        Assert.IsType<UnrelatedService>(unrelatedService); // Should not be decorated
    }

    // Test infrastructure - Interfaces

    /// <summary>
    /// Base repository interface
    /// </summary>
    public interface IRepository
    {
        string GetData(int id);
    }

    /// <summary>
    /// User repository interface
    /// </summary>
    public interface IUserRepository : IRepository
    {
        string GetUserName(int userId);
    }

    /// <summary>
    /// Product repository interface
    /// </summary>
    public interface IProductRepository : IRepository
    {
        string GetProductName(int productId);
    }

    /// <summary>
    /// Order repository interface
    /// </summary>
    public interface IOrderRepository : IRepository
    {
        string GetOrderNumber(int orderId);
    }

    /// <summary>
    /// Generic handler interface
    /// </summary>
    /// <typeparam name="T">Data type to handle</typeparam>
    public interface IHandler<T>
    {
        T Handle(T data);
    }

    /// <summary>
    /// Cache service interface
    /// </summary>
    public interface ICacheService
    {
        void Cache(string key, object value);
        T GetFromCache<T>(string key);
    }

    /// <summary>
    /// Unrelated service interface (does not implement IRepository)
    /// </summary>
    public interface IUnrelatedService
    {
        void DoSomething();
    }

    #endregion

    #region Test Infrastructure - Implementations

    /// <summary>
    /// User repository implementation
    /// </summary>
    public class UserRepository : IUserRepository
    {
        public string GetData(int id) => $"User data: {id}";
        public string GetUserName(int userId) => $"User_{userId}";
    }

    /// <summary>
    /// Product repository implementation
    /// </summary>
    public class ProductRepository : IProductRepository
    {
        public string GetData(int id) => $"Product data: {id}";
        public string GetProductName(int productId) => $"Product_{productId}";
    }

    /// <summary>
    /// Order repository implementation
    /// </summary>
    public class OrderRepository : IOrderRepository
    {
        public string GetData(int id) => $"Order data: {id}";
        public string GetOrderNumber(int orderId) => $"Order_{orderId}";
    }

    /// <summary>
    /// String handler implementation
    /// </summary>
    public class StringHandler : IHandler<string>
    {
        public string Handle(string data) => $"Handled: {data}";
    }

    /// <summary>
    /// Integer handler implementation
    /// </summary>
    public class IntHandler : IHandler<int>
    {
        public int Handle(int data) => data * 2;
    }

    /// <summary>
    /// Cache service implementation
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly Dictionary<string, object> _cache = new();

        public void Cache(string key, object value) => _cache[key] = value;
        public T GetFromCache<T>(string key) => _cache.TryGetValue(key, out var value) ? (T)value : default;
    }

    /// <summary>
    /// Unrelated service implementation
    /// </summary>
    public class UnrelatedService : IUnrelatedService
    {
        public void DoSomething() { }
    }

    #endregion

    #region Test Infrastructure - Decorators

    /// <summary>
    /// Caching repository decorator
    /// </summary>
    public class CachingRepositoryDecorator : IRepository
    {
        public IRepository Inner { get; }

        public CachingRepositoryDecorator(IRepository inner)
        {
            Inner = inner;
        }

        public string GetData(int id)
        {
            // Simulate caching logic
            return $"[Cached] {Inner.GetData(id)}";
        }
    }

    /// <summary>
    /// Logging repository decorator
    /// </summary>
    public class LoggingRepositoryDecorator : IRepository
    {
        public IRepository Inner { get; }

        public LoggingRepositoryDecorator(IRepository inner)
        {
            Inner = inner;
        }

        public string GetData(int id)
        {
            // Simulate logging logic
            return $"[Logged] {Inner.GetData(id)}";
        }
    }

    /// <summary>
    /// Service-dependent repository decorator
    /// </summary>
    public class ServiceDependentRepositoryDecorator : IRepository
    {
        public IRepository Inner { get; }
        public ICacheService CacheService { get; }

        public ServiceDependentRepositoryDecorator(IRepository inner, ICacheService cacheService)
        {
            Inner = inner;
            CacheService = cacheService;
        }

        public string GetData(int id)
        {
            return $"[ServiceDependent] {Inner.GetData(id)}";
        }
    }

    /// <summary>
    /// Specific user repository decorator
    /// </summary>
    public class SpecificUserRepositoryDecorator : IUserRepository
    {
        public IUserRepository Inner { get; }

        public SpecificUserRepositoryDecorator(IUserRepository inner)
        {
            Inner = inner;
        }

        public string GetData(int id) => $"[Specific] {Inner.GetData(id)}";
        public string GetUserName(int userId) => $"[Specific] {Inner.GetUserName(userId)}";
    }

    /// <summary>
    /// Generic logging handler decorator
    /// </summary>
    /// <typeparam name="T">Data type</typeparam>
    public class LoggingHandler<T> : IHandler<T>
    {
        public IHandler<T> Inner { get; }

        public LoggingHandler(IHandler<T> inner)
        {
            Inner = inner;
        }

        public T Handle(T data)
        {
            // Simulate logging
            return Inner.Handle(data);
        }
    }

    #endregion
} 