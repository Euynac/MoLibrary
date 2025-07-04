using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Reflection.Emit;

namespace MoLibrary.Core.Features.MoDecorator.Strategies;

/// <summary>
/// A dynamic proxy that implements an interface and delegates calls appropriately.
/// </summary>
/// <typeparam name="T">The interface type to implement</typeparam>
internal class InterfaceProxy<T> : DispatchProxy where T : class
{
    /// <summary>
    /// Gets or sets the base decorator that implements the target interface.
    /// </summary>
    public object? BaseDecorator { get; set; }

    /// <summary>
    /// Gets or sets the original service instance.
    /// </summary>
    public object? OriginalInstance { get; set; }

    /// <summary>
    /// Gets or sets the target interface type that the base decorator implements.
    /// </summary>
    public Type? TargetInterface { get; set; }

    /// <summary>
    /// Creates a proxy instance for the specified interface.
    /// </summary>
    /// <param name="baseDecorator">The base decorator</param>
    /// <param name="originalInstance">The original service instance</param>
    /// <param name="targetInterface">The target interface</param>
    /// <returns>A proxy instance</returns>
    public static T Create(object baseDecorator, object originalInstance, Type targetInterface)
    {
        var proxy = Create<T, InterfaceProxy<T>>() as InterfaceProxy<T>;
        proxy!.BaseDecorator = baseDecorator;
        proxy.OriginalInstance = originalInstance;
        proxy.TargetInterface = targetInterface;
        return (T)(object)proxy;
    }

    /// <summary>
    /// Intercepts method calls and delegates them appropriately.
    /// </summary>
    /// <param name="targetMethod">The method being called</param>
    /// <param name="args">The method arguments</param>
    /// <returns>The result of the method call</returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new ArgumentNullException(nameof(targetMethod));

        // Check if the base decorator can handle this method (implements the target interface)
        if (BaseDecorator != null && TargetInterface != null && TargetInterface.IsAssignableFrom(BaseDecorator.GetType()))
        {
            var decoratorMethod = BaseDecorator.GetType().GetMethod(targetMethod.Name, 
                targetMethod.GetParameters().Select(p => p.ParameterType).ToArray());
            
            if (decoratorMethod != null)
            {
                return decoratorMethod.Invoke(BaseDecorator, args);
            }
        }

        // If the base decorator doesn't implement the method, try the original instance
        if (OriginalInstance != null)
        {
            var originalMethod = OriginalInstance.GetType().GetMethod(targetMethod.Name, 
                targetMethod.GetParameters().Select(p => p.ParameterType).ToArray());
            
            if (originalMethod != null)
            {
                return originalMethod.Invoke(OriginalInstance, args);
            }
        }

        // If neither can handle the method, throw an exception
        throw new InvalidOperationException(
            $"Neither the decorator nor the original instance can handle method {targetMethod.Name}");
    }
}

/// <summary>
/// Decoration strategy that can decorate all services implementing a specified interface.
/// This strategy is designed to proxy services that implement the target interface,
/// enabling cross-cutting concerns like logging, caching, or authorization across multiple service types.
/// </summary>
/// <param name="targetInterface">The interface type that services must implement to be decorated</param>
/// <param name="serviceKey">Optional service key for keyed services</param>
/// <param name="decoratorType">The decorator type (must implement the target interface)</param>
/// <param name="decoratorFactory">Optional factory function for creating decorators</param>
public sealed class InterfaceProxyDecorationStrategy(
    Type targetInterface,
    string? serviceKey,
    Type? decoratorType,
    Func<object, IServiceProvider, object>? decoratorFactory)
    : DecorationStrategy(targetInterface, serviceKey)
{
    /// <summary>
    /// Gets the target interface that services must implement to be decorated.
    /// </summary>
    public Type TargetInterface { get; } = targetInterface ?? throw new ArgumentNullException(nameof(targetInterface));

    private Type? DecoratorType { get; } = decoratorType;

    private Func<object, IServiceProvider, object>? DecoratorFactory { get; } = decoratorFactory;

    /// <summary>
    /// Determines if a service type can be decorated by this strategy.
    /// A service can be decorated if it implements the target interface.
    /// </summary>
    /// <param name="serviceType">The service type to check</param>
    /// <returns>True if the service type implements the target interface</returns>
    protected override bool CanDecorate(Type serviceType)
    {
        if (TargetInterface.IsGenericTypeDefinition)
        {
            return CanDecorateGenericInterface(serviceType);
        }

        return TargetInterface.IsAssignableFrom(serviceType);
    }

    /// <summary>
    /// Creates a decorator for the specified service type.
    /// </summary>
    /// <param name="serviceType">The type of service to decorate</param>
    /// <param name="serviceKey">The service key</param>
    /// <returns>A function that creates the decorated service instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when both DecoratorType and DecoratorFactory are null</exception>
    public override Func<IServiceProvider, object?, object> CreateDecorator(Type serviceType, string serviceKey)
    {
        if (DecoratorType is not null)
        {
            var actualDecoratorType = GetActualDecoratorType(serviceType, DecoratorType);
            return CreateInterfaceProxyDecorator(serviceType, serviceKey, actualDecoratorType, null);
        }

        if (DecoratorFactory is not null)
        {
            return CreateInterfaceProxyDecorator(serviceType, serviceKey, null, DecoratorFactory);
        }

        throw new InvalidOperationException($"Both {nameof(DecoratorType)} and {nameof(DecoratorFactory)} cannot be null.");
    }

    /// <summary>
    /// Checks if a service type can be decorated when the target interface is a generic type definition.
    /// </summary>
    /// <param name="serviceType">The service type to check</param>
    /// <returns>True if the service type implements the generic interface</returns>
    private bool CanDecorateGenericInterface(Type serviceType)
    {
        if (serviceType.IsGenericTypeDefinition)
        {
            return false; // Cannot decorate open generic types directly
        }

        var implementedInterfaces = serviceType.GetInterfaces();
        
        foreach (var implementedInterface in implementedInterfaces)
        {
            if (implementedInterface.IsGenericType &&
                implementedInterface.GetGenericTypeDefinition() == TargetInterface)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the actual decorator type, handling generic types if necessary.
    /// </summary>
    /// <param name="serviceType">The service type being decorated</param>
    /// <param name="decoratorType">The decorator type</param>
    /// <returns>The concrete decorator type to use</returns>
    private Type GetActualDecoratorType(Type serviceType, Type decoratorType)
    {
        if (!decoratorType.IsGenericTypeDefinition)
        {
            return decoratorType;
        }

        // Handle generic decorator types
        if (TargetInterface.IsGenericTypeDefinition)
        {
            var implementedInterface = serviceType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == TargetInterface);

            if (implementedInterface is not null)
            {
                var genericArguments = implementedInterface.GetGenericArguments();
                try
                {
                    return decoratorType.MakeGenericType(genericArguments);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot create generic decorator type {decoratorType.Name} with arguments from {implementedInterface.Name}.", ex);
                }
            }
        }

        throw new InvalidOperationException(
            $"Cannot determine generic arguments for decorator type {decoratorType.Name} when decorating service {serviceType.Name}.");
    }

    /// <summary>
    /// Creates an interface proxy decorator that implements all interfaces of the service type.
    /// </summary>
    /// <param name="serviceType">The service type being decorated</param>
    /// <param name="serviceKey">The service key</param>
    /// <param name="decoratorType">The decorator type (if using type-based decoration)</param>
    /// <param name="decoratorFactory">The decorator factory (if using factory-based decoration)</param>
    /// <returns>A function that creates the decorated service instance</returns>
    private Func<IServiceProvider, object?, object> CreateInterfaceProxyDecorator(
        Type serviceType, 
        string serviceKey, 
        Type? decoratorType, 
        Func<object, IServiceProvider, object>? decoratorFactory)
    {
        return (serviceProvider, _) =>
        {
            var instanceToDecorate = serviceProvider.GetRequiredKeyedService(serviceType, serviceKey);

            // Create the base decorator
            object baseDecorator;
            if (decoratorType is not null)
            {
                baseDecorator = ActivatorUtilities.CreateInstance(serviceProvider, decoratorType, instanceToDecorate);
            }
            else if (decoratorFactory is not null)
            {
                baseDecorator = decoratorFactory(instanceToDecorate, serviceProvider);
            }
            else
            {
                throw new InvalidOperationException("Both decoratorType and decoratorFactory cannot be null.");
            }

            // If the service type is the same as or assignable from the base decorator type,
            // we can return the decorator directly
            if (serviceType.IsAssignableFrom(baseDecorator.GetType()))
            {
                return baseDecorator;
            }

            // Otherwise, we need to create a proxy that implements the service type's interfaces
            // and delegates calls to the base decorator
            return CreateDynamicProxy(serviceType, baseDecorator, instanceToDecorate);
        };
    }

    /// <summary>
    /// Creates a dynamic proxy that implements the service type's interfaces and delegates calls to the decorator.
    /// </summary>
    /// <param name="serviceType">The service type to implement</param>
    /// <param name="baseDecorator">The base decorator that implements the target interface</param>
    /// <param name="originalInstance">The original service instance</param>
    /// <returns>A proxy object that implements the service type</returns>
    private object CreateDynamicProxy(Type serviceType, object baseDecorator, object originalInstance)
    {
        // For simplicity, we'll use a more direct approach here
        // Create a wrapper that implements the service type and delegates appropriately
        
        if (serviceType.IsInterface)
        {
            return CreateInterfaceProxy(serviceType, baseDecorator, originalInstance);
        }
        
        // For non-interface types, this is more complex and may require class proxies
        // For now, return the original decorator and let the casting fail with a better error message
        throw new InvalidOperationException(
            $"Cannot create interface proxy for non-interface type {serviceType.Name}. " +
            $"The decorator {baseDecorator.GetType().Name} must implement {serviceType.Name} directly.");
    }

    /// <summary>
    /// Creates a proxy for an interface type.
    /// </summary>
    /// <param name="interfaceType">The interface type to implement</param>
    /// <param name="baseDecorator">The base decorator</param>
    /// <param name="originalInstance">The original service instance</param>
    /// <returns>A proxy object that implements the interface</returns>
    private object CreateInterfaceProxy(Type interfaceType, object baseDecorator, object originalInstance)
    {
        // Use the static Create method of InterfaceProxy<T>
        var createMethod = typeof(InterfaceProxy<>)
            .MakeGenericType(interfaceType)
            .GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;
        
        return createMethod.Invoke(null, new object[] { baseDecorator, originalInstance, TargetInterface })!;
    }
} 