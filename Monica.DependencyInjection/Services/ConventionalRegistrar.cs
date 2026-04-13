using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Abstractions.Internal;
using Monica.DependencyInjection.Annotations;
using Monica.DependencyInjection.Models;
using Monica.DependencyInjection.Models.Internal;
using Monica.DependencyInjection.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.DependencyInjection.Services;
/// <summary>
/// Registers discovered Monica services using lifetime markers and exposure attributes.
/// </summary>
internal class ConventionalRegistrar(ModuleDependencyInjectionOption option, DependencyInjectionDiagnosticsRegistry? diagnosticsRegistry = null) : IConventionalRegistrar
{
    private ILogger Logger => option.Logger;
    
    /// <summary>
    /// Registers a single type into the service collection based on its attributes and lifetime.
    /// </summary>
    /// <param name="services">The service collection to which the dependency will be added.</param>
    /// <param name="type">The type to be registered.</param>
    public virtual void AddType(IServiceCollection services, Type type)
    {
        // TODO: Support automatic registration of generic types through configuration.
        if(type is not { IsClass: true, IsAbstract: false, IsGenericType: false }) return;

        var dependencyAttribute = GetDependencyAttributeOrNull(type);
        var lifeTime = GetLifeTimeOrNull(type, dependencyAttribute);
        if (lifeTime == null)
        {
            return;
        }

        var typeName = type.Name;
        var lifetimeSource = ResolveLifetimeSource(type, dependencyAttribute);
        var registrationMode = ResolveRegistrationMode(dependencyAttribute);
        var shouldEmitDiagnostics = option.EnableAutoRegistrationDiagnostics;
        var shouldCaptureDiagnostics = shouldEmitDiagnostics && diagnosticsRegistry != null;

        var exposedServiceAndKeyedServiceTypes = GetExposedKeyedServiceTypes(type)
            .Concat(GetExposedServiceTypes(type).Select(t => new ServiceIdentifier(t)))
            .ToList();
        var exposedServicesByKey = exposedServiceAndKeyedServiceTypes.ToLookup(item => item.ServiceKey);
        var autoRegistrationIssues = shouldEmitDiagnostics
            ? CreateAutoRegistrationIssues(type, lifeTime.Value, lifetimeSource, exposedServiceAndKeyedServiceTypes)
            : [];

        if (shouldEmitDiagnostics)
        {
            if (exposedServiceAndKeyedServiceTypes.Count == 0)
            {
                Logger.LogError("Failed to auto-register type: {TypeName} {Lifetime}", typeName, lifeTime);
            }
            else if (autoRegistrationIssues.Any(item => item.Kind == DependencyInjectionAutoRegistrationIssueKind.ConcreteTypeOnlyExposure))
            {
                Logger.LogWarning("Only the concrete type was registered: {TypeName} {Lifetime}", typeName, lifeTime);
            }
            else
            {
                Logger.LogInformation("Auto-registered: {TypeName}->{ServiceTypes} {Lifetime}",
                    typeName,
                    $"[{exposedServiceAndKeyedServiceTypes.Select(p => p.ServiceType.Name).StringJoin(", ")}]",
                    lifeTime);
            }
        }

        if (shouldCaptureDiagnostics && exposedServiceAndKeyedServiceTypes.Count == 0 && autoRegistrationIssues.Count > 0)
        {
            diagnosticsRegistry!.RecordStandaloneAutoRegistrationIssues(autoRegistrationIssues);
        }
        
        foreach (var exposedServiceType in exposedServiceAndKeyedServiceTypes)
        {
            var allExposingServiceTypes = exposedServicesByKey[exposedServiceType.ServiceKey].ToList();
            var hadExistingDescriptor = shouldCaptureDiagnostics &&
                services.Any(existing =>
                    existing.MatchesServiceIdentity(exposedServiceType.ServiceType, exposedServiceType.ServiceKey));
            var serviceDescriptor = CreateServiceDescriptor(
                type,
                exposedServiceType.ServiceKey,
                exposedServiceType.ServiceType,
                allExposingServiceTypes,
                lifeTime.Value
            );
            var descriptorWasAdded = ApplyRegistrationMode(services, serviceDescriptor, registrationMode, hadExistingDescriptor);

            if (shouldCaptureDiagnostics && descriptorWasAdded)
            {
                diagnosticsRegistry!.RecordConventionalRegistration(
                    serviceDescriptor,
                    type,
                    lifeTime.Value,
                    lifetimeSource,
                    registrationMode,
                    hadExistingDescriptor && registrationMode == DependencyInjectionAutoRegistrationMode.Replace
                        ? DependencyInjectionAutoRegistrationOutcome.ReplacedExisting
                        : DependencyInjectionAutoRegistrationOutcome.Added,
                    [.. exposedServiceAndKeyedServiceTypes],
                    autoRegistrationIssues);
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

    protected virtual DependencyInjectionLifetimeSource ResolveLifetimeSource(
        Type type,
        DependencyAttribute? dependencyAttribute)
    {
        if (dependencyAttribute?.Lifetime != null)
        {
            return DependencyInjectionLifetimeSource.DependencyAttribute;
        }

        if (typeof(ITransientDependency).IsAssignableFrom(type))
        {
            return DependencyInjectionLifetimeSource.TransientMarkerInterface;
        }

        if (typeof(ISingletonDependency).IsAssignableFrom(type))
        {
            return DependencyInjectionLifetimeSource.SingletonMarkerInterface;
        }

        if (typeof(IScopedDependency).IsAssignableFrom(type))
        {
            return DependencyInjectionLifetimeSource.ScopedMarkerInterface;
        }

        return DependencyInjectionLifetimeSource.Unknown;
    }

    protected virtual DependencyInjectionAutoRegistrationMode ResolveRegistrationMode(DependencyAttribute? dependencyAttribute)
    {
        if (dependencyAttribute?.ReplaceServices == true)
        {
            return DependencyInjectionAutoRegistrationMode.Replace;
        }

        if (dependencyAttribute?.TryRegister == true)
        {
            return DependencyInjectionAutoRegistrationMode.TryAdd;
        }

        return DependencyInjectionAutoRegistrationMode.Add;
    }

    private bool ApplyRegistrationMode(
        IServiceCollection services,
        ServiceDescriptor descriptor,
        DependencyInjectionAutoRegistrationMode registrationMode,
        bool hadExistingDescriptor)
    {
        switch (registrationMode)
        {
            case DependencyInjectionAutoRegistrationMode.Replace:
                services.Replace(descriptor);
                return true;
            case DependencyInjectionAutoRegistrationMode.TryAdd:
                services.TryAdd(descriptor);
                return !hadExistingDescriptor;
            default:
                services.Add(descriptor);
                return true;
        }
    }

    private IReadOnlyList<DependencyInjectionAutoRegistrationIssueInfo> CreateAutoRegistrationIssues(
        Type sourceImplementationType,
        ServiceLifetime lifetime,
        DependencyInjectionLifetimeSource lifetimeSource,
        IReadOnlyList<ServiceIdentifier> exposedServices)
    {
        if (exposedServices.Count == 0)
        {
            return [CreateAutoRegistrationIssue(
                sourceImplementationType,
                lifetime,
                lifetimeSource,
                DependencyInjectionAutoRegistrationIssueKind.MissingExposedServices,
                exposedServices)];
        }

        if (exposedServices is [{ ServiceType: { } serviceType }] && serviceType == sourceImplementationType)
        {
            return [CreateAutoRegistrationIssue(
                sourceImplementationType,
                lifetime,
                lifetimeSource,
                DependencyInjectionAutoRegistrationIssueKind.ConcreteTypeOnlyExposure,
                exposedServices)];
        }

        return [];
    }

    private DependencyInjectionAutoRegistrationIssueInfo CreateAutoRegistrationIssue(
        Type sourceImplementationType,
        ServiceLifetime lifetime,
        DependencyInjectionLifetimeSource lifetimeSource,
        DependencyInjectionAutoRegistrationIssueKind kind,
        IReadOnlyList<ServiceIdentifier> exposedServices)
    {
        return new DependencyInjectionAutoRegistrationIssueInfo
        {
            Severity = kind == DependencyInjectionAutoRegistrationIssueKind.MissingExposedServices
                ? DependencyInjectionDiagnosticSeverity.Error
                : DependencyInjectionDiagnosticSeverity.Warning,
            Kind = kind,
            SourceImplementationType = sourceImplementationType.GetCleanFullName(),
            SourceImplementationTypeDisplayName = sourceImplementationType.GetCleanName(),
            SourceImplementationAssemblyName = sourceImplementationType.Assembly.GetName().Name,
            Lifetime = lifetime,
            LifetimeSource = lifetimeSource,
            ExposedServices = exposedServices
                .Select(item => new DependencyInjectionExposedServiceInfo
                {
                    ServiceType = item.ServiceType.GetCleanFullName(),
                    ServiceTypeDisplayName = item.ServiceType.GetCleanName(),
                    IsKeyedService = item.ServiceKey != null,
                    ServiceKey = ServiceDescriptorDiagnosticsExtensions.FormatServiceKey(item.ServiceKey)
                })
                .ToArray()
        };
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
        var requiresCachedServiceProviderAccess = RequiresCachedServiceProviderAccess(implementationType);

        if (requiresCachedServiceProviderAccess && lifeTime == ServiceLifetime.Singleton)
        {
            throw new InvalidOperationException(
                $"{implementationType.FullName} can not be registered as Singleton because it requires {nameof(ICachedServiceProvider)}.");
        }

        // TODO: Support automatic registration of generic types.
        //if (implementationType.IsGenericType)
        //{
        //    implementationType = implementationType.GetGenericTypeDefinition();
        //}

        //if (exposingServiceType.IsGenericType)
        //{
        //    exposingServiceType = exposingServiceType.GetGenericTypeDefinition();
        //}

        // TODO: Revisit whether this redirection block is still necessary.
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
        return CreateDirectServiceDescriptor(
            implementationType,
            serviceKey,
            exposingServiceType,
            lifeTime,
            requiresCachedServiceProviderAccess);
    }

    protected virtual bool RequiresCachedServiceProviderAccess(Type implementationType)
    {
        return typeof(ICachedServiceProviderAccessor).IsAssignableFrom(implementationType);
    }

    protected virtual ServiceDescriptor CreateDirectServiceDescriptor(
        Type implementationType,
        object? serviceKey,
        Type exposingServiceType,
        ServiceLifetime lifeTime,
        bool requiresCachedServiceProviderAccess)
    {
        if (!requiresCachedServiceProviderAccess)
        {
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

        return serviceKey == null
            ? ServiceDescriptor.Describe(
                exposingServiceType,
                provider => CreateImplementationInstance(provider, implementationType),
                lifeTime
            )
            : ServiceDescriptor.DescribeKeyed(
                exposingServiceType,
                serviceKey,
                (provider, _) => CreateImplementationInstance(provider, implementationType),
                lifeTime
            );
    }

    protected virtual object CreateImplementationInstance(IServiceProvider provider, Type implementationType)
    {
        var instance = ActivatorUtilities.CreateInstance(provider, implementationType);

        if (instance is ICachedServiceProviderAccessor accessor)
        {
            accessor.CachedServiceProvider = provider.GetRequiredService<ICachedServiceProvider>();
        }

        return instance;
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
