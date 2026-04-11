using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Monica.Tool.Extensions;

namespace Monica.DependencyInjection.Services.Support;

/// <summary>
/// Provides safe, allocation-light helpers for inspecting service descriptors without invoking factories.
/// </summary>
internal static class ServiceDescriptorDiagnosticsExtensions
{
    /// <summary>
    /// Gets the resolved implementation instance when the descriptor is instance-backed.
    /// </summary>
    public static object? GetResolvedImplementationInstance(this ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.IsKeyedService
            ? descriptor.KeyedImplementationInstance
            : descriptor.ImplementationInstance;
    }

    /// <summary>
    /// Gets the resolved implementation factory delegate when the descriptor is factory-backed.
    /// </summary>
    public static Delegate? GetResolvedImplementationFactory(this ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.IsKeyedService
            ? descriptor.KeyedImplementationFactory
            : descriptor.ImplementationFactory;
    }

    /// <summary>
    /// Gets the best-effort implementation type without invoking factories.
    /// </summary>
    public static Type? GetResolvedImplementationType(this ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType
                ?? descriptor.KeyedImplementationInstance?.GetType()
                ?? TryInferImplementationType(descriptor.KeyedImplementationFactory)
            : descriptor.ImplementationType
                ?? descriptor.ImplementationInstance?.GetType()
                ?? TryInferImplementationType(descriptor.ImplementationFactory);
    }

    /// <summary>
    /// Gets the implementation kind represented by the descriptor.
    /// </summary>
    public static DependencyInjection.Models.DependencyInjectionDescriptorImplementationKind GetImplementationKind(this ServiceDescriptor descriptor)
    {
        if (descriptor.GetResolvedImplementationInstance() != null)
        {
            return DependencyInjection.Models.DependencyInjectionDescriptorImplementationKind.Instance;
        }

        if (descriptor.GetResolvedImplementationFactory() != null)
        {
            return DependencyInjection.Models.DependencyInjectionDescriptorImplementationKind.Factory;
        }

        return DependencyInjection.Models.DependencyInjectionDescriptorImplementationKind.Type;
    }

    /// <summary>
    /// Gets a readable display string for the service key.
    /// </summary>
    public static string? GetServiceKeyDisplay(this ServiceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return FormatServiceKey(descriptor.ServiceKey);
    }

    /// <summary>
    /// Gets a readable display string for a service key object.
    /// </summary>
    public static string? FormatServiceKey(object? serviceKey)
    {
        return serviceKey?.ToString();
    }

    /// <summary>
    /// Gets a stable, human-readable representation of the backing factory delegate.
    /// </summary>
    public static string? GetFactoryDisplay(this ServiceDescriptor descriptor)
    {
        var factory = descriptor.GetResolvedImplementationFactory();
        if (factory == null)
        {
            return null;
        }

        var declaringType = factory.Method.DeclaringType;
        var declaringTypeName = declaringType == null
            ? factory.Method.Name
            : $"{declaringType.GetCleanFullName()}.{factory.Method.Name}";

        return $"{declaringTypeName}()";
    }

    /// <summary>
    /// Determines whether two descriptors represent the same service identity.
    /// </summary>
    public static bool MatchesServiceIdentity(this ServiceDescriptor descriptor, Type serviceType, object? serviceKey)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(serviceType);

        return descriptor.ServiceType == serviceType && Equals(descriptor.ServiceKey, serviceKey);
    }

    private static Type? TryInferImplementationType(Delegate? factory)
    {
        if (factory == null)
        {
            return null;
        }

        if (factory.Method.ReturnType != typeof(object))
        {
            return factory.Method.ReturnType;
        }

        if (factory.Target == null)
        {
            return null;
        }

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return TryExtractType(factory.Target, depth: 2, visited);
    }

    // Many framework and compiler-generated registration closures keep the implementation type
    // inside nested captured objects. Walk a shallow object graph so diagnostics can show that
    // type without ever invoking the factory.
    private static Type? TryExtractType(object target, int depth, ISet<object> visited)
    {
        if (depth < 0 || !visited.Add(target))
        {
            return null;
        }

        if (target is Type type)
        {
            return type;
        }

        if (target is ServiceDescriptor descriptor)
        {
            return descriptor.GetResolvedImplementationType();
        }

        if (target is string || target.GetType().IsPrimitive || target.GetType().IsEnum)
        {
            return null;
        }

        foreach (var field in target.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.GetValue(target) is not { } value)
            {
                continue;
            }

            var resolved = TryExtractType(value, depth - 1, visited);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }
}
