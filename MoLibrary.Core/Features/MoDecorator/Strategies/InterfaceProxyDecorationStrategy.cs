using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Features.MoDecorator.Strategies;

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
            return TypeDecorator(serviceType, serviceKey, actualDecoratorType);
        }

        if (DecoratorFactory is not null)
        {
            return FactoryDecorator(serviceType, serviceKey, DecoratorFactory);
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
} 