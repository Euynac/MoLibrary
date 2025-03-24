using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.Attributes;
using MoLibrary.DependencyInjection.CoreInterfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DependencyInjection.Implements;
/// <summary>
/// Provides default implementation for registering dependencies in an assembly or specific types.
/// Implements the <see cref="IConventionalRegistrar"/> interface.
/// </summary>
public class DefaultConventionalRegistrar(MoDependencyOption option) : IConventionalRegistrar
{
    private ILogger logger => option.Logger;
    
    /// <summary>
    /// Registers all classes in the specified assembly into the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the dependencies will be added.</param>
    /// <param name="assembly">The assembly containing the types to be registered.</param>
    public virtual void AddAssembly(IServiceCollection services, Assembly assembly)
    {
        //TODO 支持泛型自动注册，但需要进行配置实现
        foreach (var type in assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false, IsGenericType: false }))
        {
            AddType(services, type);
        }
    }
    /// <summary>
    /// Registers the specified types into the service collection.
    /// </summary>
    /// <param name="services">The service collection to which the dependencies will be added.</param>
    /// <param name="types">The types to be registered.</param>
    public virtual void AddTypes(IServiceCollection services, params Type[] types)
    {
        foreach (var type in types)
        {
            AddType(services, type);
        }
    }
    /// <summary>
    /// Registers a single type into the service collection based on its attributes and lifetime.
    /// </summary>
    /// <param name="services">The service collection to which the dependency will be added.</param>
    /// <param name="type">The type to be registered.</param>
    public virtual void AddType(IServiceCollection services, Type type)
    {
        var dependencyAttribute = GetDependencyAttributeOrNull(type);
        var lifeTime = GetLifeTimeOrNull(type, dependencyAttribute);
        if (lifeTime == null)
        {
            return;
        }

        var typeName = type.Name;

        var exposedServiceAndKeyedServiceTypes = GetExposedKeyedServiceTypes(type)
            .Concat(GetExposedServiceTypes(type).Select(t => new ServiceIdentifier(t)))
            .ToList();

       
        if (option.EnableDebug)
        {
            if (exposedServiceAndKeyedServiceTypes.Count == 0)
            {
                logger.LogError("未能自动注册成功的类型：{name} {lifetime}", typeName, lifeTime);
            }
            else if (exposedServiceAndKeyedServiceTypes is [{ServiceType: { } typeSelf}] && typeSelf.Name == typeName)
            {
                
                logger.LogWarning("仅注册了本身类型：{name} {lifetime}", typeName, lifeTime);
            }
            else
            {
                
                logger.LogInformation("自动注册：{name}->{serviceType} {lifetime}", typeName,$"[{exposedServiceAndKeyedServiceTypes.Select(p=>p.ServiceType.Name).StringJoin(", ")}]", lifeTime);
            }
        }
        
        foreach (var exposedServiceType in exposedServiceAndKeyedServiceTypes)
        {
            var allExposingServiceTypes = exposedServiceType.ServiceKey == null
                ? exposedServiceAndKeyedServiceTypes.Where(x => x.ServiceKey == null).ToList()
                : exposedServiceAndKeyedServiceTypes.Where(x => x.ServiceKey?.ToString() == exposedServiceType.ServiceKey?.ToString()).ToList();
            var serviceDescriptor = CreateServiceDescriptor(
                type,
                exposedServiceType.ServiceKey,
                exposedServiceType.ServiceType,
                allExposingServiceTypes,
                lifeTime.Value
            );
            if (dependencyAttribute?.ReplaceServices == true)
            {
                services.Replace(serviceDescriptor);
            }
            else if (dependencyAttribute?.TryRegister == true)
            {
                services.TryAdd(serviceDescriptor);
            }
            else
            {
                services.Add(serviceDescriptor);
            }
        }
    }
    /// <summary>
    /// Retrieves the <see cref="DependencyAttribute"/> from the specified type, if available.
    /// </summary>
    /// <param name="type">The type to inspect for the attribute.</param>
    /// <returns>The <see cref="DependencyAttribute"/> if found; otherwise, null.</returns>
    protected virtual DependencyAttribute? GetDependencyAttributeOrNull(Type type)
    {
        return type.GetCustomAttribute<DependencyAttribute>(true);
    }
    /// <summary>
    /// Determines the service lifetime for the specified type based on its attributes or class hierarchy.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="dependencyAttribute">The dependency attribute associated with the type.</param>
    /// <returns>The determined <see cref="ServiceLifetime"/> if available; otherwise, null.</returns>
    protected virtual ServiceLifetime? GetLifeTimeOrNull(Type type, DependencyAttribute? dependencyAttribute)
    {
        return dependencyAttribute?.Lifetime ?? GetServiceLifetimeFromClassHierarchy(type);
    }
    /// <summary>
    /// Determines the service lifetime based on the class hierarchy of the specified type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The determined <see cref="ServiceLifetime"/> if available; otherwise, null.</returns>
    protected virtual ServiceLifetime? GetServiceLifetimeFromClassHierarchy(Type type)
    {
        if (typeof(ITransientDependency).IsAssignableFrom(type))
        {
            return ServiceLifetime.Transient;
        }
        if (typeof(ISingletonDependency).IsAssignableFrom(type))
        {
            return ServiceLifetime.Singleton;
        }
        if (typeof(IScopedDependency).IsAssignableFrom(type))
        {
            return ServiceLifetime.Scoped;
        }
        return null;
    }
  
    /// <summary>
    /// Retrieves the list of exposed service types for the specified type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A list of exposed service types.</returns>
    protected virtual List<Type> GetExposedServiceTypes(Type type)
    {
        return ExposedServiceExplorer.GetExposedServices(type);
    }
    /// <summary>
    /// Retrieves the list of exposed keyed service types for the specified type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A list of exposed keyed service types.</returns>
    protected virtual List<ServiceIdentifier> GetExposedKeyedServiceTypes(Type type)
    {
        return ExposedServiceExplorer.GetExposedKeyedServices(type);
    }
    /// <summary>
    /// Creates a <see cref="ServiceDescriptor"/> for the specified implementation and service type.
    /// </summary>
    /// <param name="implementationType">The type implementing the service.</param>
    /// <param name="serviceKey">The key associated with the service, if any.</param>
    /// <param name="exposingServiceType">The type of the service being exposed.</param>
    /// <param name="allExposingServiceTypes">All service types being exposed.</param>
    /// <param name="lifeTime">The lifetime of the service.</param>
    /// <returns>A <see cref="ServiceDescriptor"/> for the service.</returns>
    protected virtual ServiceDescriptor CreateServiceDescriptor(
        Type implementationType,
        object? serviceKey,
        Type exposingServiceType,
        List<ServiceIdentifier> allExposingServiceTypes,
        ServiceLifetime lifeTime)
    {
        //TODO 泛型自动注册
        //if (implementationType.IsGenericType)
        //{
        //    implementationType = implementationType.GetGenericTypeDefinition();
        //}

        //if (exposingServiceType.IsGenericType)
        //{
        //    exposingServiceType = exposingServiceType.GetGenericTypeDefinition();
        //}

        //TODO 研究这段是否有必要
        if (lifeTime.EqualsAny(ServiceLifetime.Singleton, ServiceLifetime.Scoped))
        {
            var redirectedType = GetRedirectedTypeOrNull(
                implementationType,
                exposingServiceType,
                allExposingServiceTypes
            );
            if (redirectedType != null)
            {
                return serviceKey == null
                    ? ServiceDescriptor.Describe(
                        exposingServiceType,
                        provider => provider.GetService(redirectedType)!,
                        lifeTime
                    )
                    : ServiceDescriptor.DescribeKeyed(
                        exposingServiceType,
                        serviceKey,
                        (provider, key) =>
                        {
                            if (provider is IKeyedServiceProvider keyedServiceProvider)
                            {
                                return keyedServiceProvider.GetKeyedService(redirectedType, key)!;
                            }

                            throw new InvalidOperationException("This service provider doesn't support keyed services.");
                        },
                        lifeTime
                    );
            }
        }
        return serviceKey == null
            ? ServiceDescriptor.Describe(
                exposingServiceType,
                implementationType,
                lifeTime
            )
            : ServiceDescriptor.DescribeKeyed(
                exposingServiceType,
                serviceKey,
                implementationType,
                lifeTime
            );
    }
    /// <summary>
    /// Determines the redirected type for a service, if applicable.
    /// </summary>
    /// <param name="implementationType">The type implementing the service.</param>
    /// <param name="exposingServiceType">The type of the service being exposed.</param>
    /// <param name="allExposingKeyedServiceTypes">All keyed service types being exposed.</param>
    /// <returns>The redirected type, if applicable; otherwise, null.</returns>
    protected virtual Type? GetRedirectedTypeOrNull(
        Type implementationType,
        Type exposingServiceType,
        List<ServiceIdentifier> allExposingKeyedServiceTypes)
    {
        if (allExposingKeyedServiceTypes.Count < 2)
        {
            return null;
        }
        if (exposingServiceType == implementationType)
        {
            return null;
        }
        if (allExposingKeyedServiceTypes.Any(t => t.ServiceType == implementationType))
        {
            return implementationType;
        }
        return allExposingKeyedServiceTypes.FirstOrDefault(
            t => t.ServiceType != exposingServiceType && exposingServiceType.IsAssignableFrom(t.ServiceType)
        ).ServiceType;
    }
}
