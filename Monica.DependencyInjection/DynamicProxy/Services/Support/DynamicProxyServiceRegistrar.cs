using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.DynamicProxy.Models;
using Monica.DependencyInjection.DynamicProxy.Providers.Castle;
using Monica.DependencyInjection.Models;
using Monica.DependencyInjection.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.DependencyInjection.DynamicProxy.Services.Support;

/// <summary>
/// Rewrites service registrations to apply Monica dynamic-proxy interceptors.
/// </summary>
internal static class DynamicProxyServiceRegistrar
{
    /// <summary>
     /// Attempts to determine the implementation type of a service based on the provided factory function.
     /// </summary>
    /// <param name="func">A factory function that creates the service instance.</param>
    /// <returns>
    /// The determined implementation type of the service. If the implementation type cannot be inferred,
    /// the return type of the factory function is returned.
    /// </returns>
    /// <remarks>
    /// This method inspects the factory function to extract the implementation type, particularly in cases
    /// where the factory function is a closure containing a field of type <see cref="Type"/>. If no specific
    /// implementation type can be identified, the method defaults to returning the return type of the factory function.
    /// </remarks>
    private static Type AutoFindImplementationType(Func<IServiceProvider, object> func)
    {
        if (func.Method.ReturnType != typeof(object)) return func.Method.ReturnType;
        if (func is { Target: { } closureFuncObj })
        {
            var typeField = closureFuncObj.GetType().GetFields().FirstOrDefault(p => p.FieldType == typeof(Type));
            if (typeField?.GetValue(closureFuncObj) is Type implType)
            {
                return implType;
            }
        }

        return func.Method.ReturnType;
    }

    internal class RegisterContext
    {
        public RegisterContext(ModuleDynamicProxyOption option, bool shouldInjectCachedServiceProvider,
            ServiceDescriptor oldDescriptor,
            Type implementType,
            List<Type> interceptorTypes, ERegisterWays way)
        {
            Option = option;
            ShouldInjectCachedServiceProvider = shouldInjectCachedServiceProvider;
            OldDescriptor = oldDescriptor;
            ImplementType = implementType;
            InterceptorTypes = interceptorTypes;
            Way = way;
            CalculateProxyKind();
            ValidateServiceInject();
        }

        public ModuleDynamicProxyOption Option { get; }
        public bool ShouldInjectCachedServiceProvider { get; }
        public ServiceDescriptor OldDescriptor { get; }
        public Type ServiceType => OldDescriptor.ServiceType;
        public Type ImplementType { get; }
        public List<Type> InterceptorTypes { get; }
        public ERegisterWays Way { get; }
        public EDynamicProxyKind Kind { get; set; }
        public void CalculateProxyKind()
        {
            if (Option.ConfiguredProxyKinds.TryGetValue(ServiceType, out var kind))
            {
                Kind = kind;
                return;
            }

            Kind = ServiceType.IsInterface ? EDynamicProxyKind.InterfaceProxy : EDynamicProxyKind.ClassProxy;
        }

        /// <summary>
        /// Validate service injection. If service is not injectable, throws an exception. If service inject may contain hidden trouble,log warning.
        /// Judge base on the proxy kind and the service register way and the Descriptor info.
        /// </summary>
        /// <remarks>
        /// For example, if the proxy kind is InterfaceProxy while the ServiceType is not interface, then the service is not injectable.
        /// If the proxy kind is ClassProxy and the service register way is Factory or Instance, then the service may contain hidden trouble.
        /// </remarks>
        public void ValidateServiceInject()
        {
            if (Kind == EDynamicProxyKind.InterfaceProxy && !ServiceType.IsInterface)
                throw new InvalidOperationException(
                    $"ServiceType '{ServiceType.FullName}' must be an interface for InterfaceProxy.");

            if (Kind == EDynamicProxyKind.ClassProxy && Way is ERegisterWays.Factory or ERegisterWays.Instance)
                Option.Logger.LogWarning(
                    $"ServiceType '{ServiceType.FullName}' registered as {Way} when using dynamic proxy may contain hidden trouble when using ClassProxy, because when using class proxy with target, fields state can not be saved and it will give inconsistent state");
        }
    }

    public enum ERegisterWays
    {
        Normal,
        Factory,
        Instance
    }

    /// <summary>
    /// Applies the configured interceptors to the services in the collection.
    /// </summary>
    /// <param name="collection">The service collection.</param>
    /// <param name="option"></param>
    internal static void ApplyInterceptors(IServiceCollection collection, ModuleDynamicProxyOption option)
    {
        var diagnosticsRegistry = DependencyInjectionDiagnosticsRegistryLocator.GetRegistry(collection);

        for (var index = collection.Count - 1; index >= 0; index--)
        {
            var oldDescriptor = collection[index];
            var implementType = oldDescriptor.ImplementationType;
            var way = ERegisterWays.Normal;

            if (oldDescriptor.ImplementationFactory is { } factory)
            {
                implementType = AutoFindImplementationType(factory);
                way = ERegisterWays.Factory;
            }
            else if (oldDescriptor.ImplementationInstance is { } instance)
            {
                implementType = instance.GetType();
                way = ERegisterWays.Instance;
            }

            if (implementType is null) continue;

            var interceptorTypes = new List<Type>();

            foreach (var registration in option.InterceptorRegistrations)
            {
                if (!registration.ShouldIntercept.Invoke(new ProxyBuildContext(implementType, oldDescriptor)))
                {
                    continue;
                }

                interceptorTypes.Add(registration.GetAdapterType());
            }
            if (interceptorTypes.Count <= 0) continue;

            collection.RemoveAt(index);

            var shouldInjectCachedServiceProvider = implementType.IsImplementInterface<ICachedServiceProviderAccessor>();
            var context = new RegisterContext(option, shouldInjectCachedServiceProvider, oldDescriptor, implementType, interceptorTypes, way);

            switch (way)
            {
                case ERegisterWays.Factory:
                    AddFactoryRegister(context);
                    break;
                case ERegisterWays.Instance:
                    AddInstanceRegister(context);
                    break;
                default:
                    AddNormalRegister(context);
                    break;
            }
        }

        return;

        void InjectCachedServiceProvider(object proxiedObject, IServiceProvider provider, RegisterContext context)
        {
            if (context.ShouldInjectCachedServiceProvider)
            {
                ((ICachedServiceProviderAccessor) proxiedObject).CachedServiceProvider = provider.GetRequiredService<ICachedServiceProvider>();
            }
        }

        // Avoid registering the same interceptor type more than once.
        IInterceptor[] GetInterceptors(IServiceProvider provider, RegisterContext context)
        {
            var types = context.InterceptorTypes;
            if (types.Count > 1)
            {
                types = types.DistinctBy(p => p.FullName).ToList();
            }
            return types
                .Select(p => (IInterceptor)ActivatorUtilities.CreateInstance(provider, p))
                .ToArray();
        }

        void AddInstanceRegister(RegisterContext context)
        {
            var proxiedDescriptor = new ServiceDescriptor(context.OldDescriptor.ServiceType, context.OldDescriptor.ServiceKey,
                (provider, o) =>
                {
                    var instance = context.OldDescriptor.ImplementationInstance!;
                    var proxyGenerator = provider.GetRequiredService<ServiceProviderProxyGenerator>();
                    var interceptors = GetInterceptors(provider, context);
                    object proxiedObject;
                    switch (context.Kind)
                    {
                        case EDynamicProxyKind.ClassProxy:
                            proxiedObject = proxyGenerator.CreateClassProxyWithTargetAndDI(provider,
                                context.ImplementType, null, instance, new ProxyGenerationOptions(), null,
                                interceptors);
                            InjectCachedServiceProvider(proxiedObject, provider, context);
                            break;
                        case EDynamicProxyKind.InterfaceProxy:
                            InjectCachedServiceProvider(instance, provider, context);
                            proxiedObject = proxyGenerator.CreateInterfaceProxyWithTarget(
                                context.OldDescriptor.ServiceType, instance, interceptors);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    return proxiedObject;
                }, context.OldDescriptor.Lifetime);
            collection.Add(proxiedDescriptor);
            diagnosticsRegistry?.TransferConventionalRegistration(context.OldDescriptor, proxiedDescriptor,
                CreateRewriteInfo(context));
        }

        void AddFactoryRegister(RegisterContext context)
        {
            var factory = context.OldDescriptor.ImplementationFactory!;
            var proxiedDescriptor = new ServiceDescriptor(context.OldDescriptor.ServiceType, context.OldDescriptor.ServiceKey,
                (provider, o) =>
                {
                    var proxyGenerator = provider.GetRequiredService<ServiceProviderProxyGenerator>();
                    var interceptors = GetInterceptors(provider, context);
                    var targetFromFactory = factory.Invoke(provider);
                    object? proxiedObject;
                    // TODO: Property injection is not supported here because the factory result is created only once.
                    switch (context.Kind)
                    {
                        case EDynamicProxyKind.ClassProxy:
                            proxiedObject = proxyGenerator.CreateClassProxyWithTargetAndDI(provider,
                                context.ImplementType, null, targetFromFactory, new ProxyGenerationOptions(), null,
                                interceptors);
                            InjectCachedServiceProvider(proxiedObject, provider, context);
                            break;
                        case EDynamicProxyKind.InterfaceProxy:
                            InjectCachedServiceProvider(targetFromFactory, provider, context);
                            proxiedObject =
                                proxyGenerator.CreateInterfaceProxyWithTarget(context.OldDescriptor.ServiceType,
                                    targetFromFactory, interceptors);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    return proxiedObject;
                }, context.OldDescriptor.Lifetime);
            collection.Add(proxiedDescriptor);
            diagnosticsRegistry?.TransferConventionalRegistration(context.OldDescriptor, proxiedDescriptor,
                CreateRewriteInfo(context));
        }

        void AddNormalRegister(RegisterContext context)
        {
            var proxiedDescriptor = new ServiceDescriptor(context.OldDescriptor.ServiceType, context.OldDescriptor.ServiceKey,
                (provider, o) =>
                {
                    var proxyGenerator = provider.GetRequiredService<ServiceProviderProxyGenerator>();
                    var interceptors = GetInterceptors(provider, context);
                    object? proxiedObject;
                    switch (context.Kind)
                    {
                        case EDynamicProxyKind.ClassProxy:
                            proxiedObject = proxyGenerator.CreateClassProxyAndDI(provider, context.ImplementType, null,
                                new ProxyGenerationOptions(), null, interceptors);
                            InjectCachedServiceProvider(proxiedObject, provider, context);
                            break;
                        case EDynamicProxyKind.InterfaceProxy:
                            proxiedObject = ActivatorUtilities.CreateInstance(provider, context.ImplementType);
                            InjectCachedServiceProvider(proxiedObject, provider, context); // Run before creating the proxied instance because interface proxies cannot receive the property assignment later.
                            proxiedObject =
                                proxyGenerator.CreateInterfaceProxyWithTarget(context.OldDescriptor.ServiceType, proxiedObject, interceptors);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                  
                    return proxiedObject;
                }, context.OldDescriptor.Lifetime);
            collection.Add(proxiedDescriptor);
            diagnosticsRegistry?.TransferConventionalRegistration(context.OldDescriptor, proxiedDescriptor,
                CreateRewriteInfo(context));
        }

    }

    private static DependencyInjectionDescriptorRewriteInfo CreateRewriteInfo(RegisterContext context)
    {
        var interceptorTypes = context.InterceptorTypes
            .DistinctBy(type => type.FullName)
            .ToArray();
        var interceptorDisplayNames = interceptorTypes
            .Select(type => type.GetCleanName())
            .ToArray();
        var summary = interceptorDisplayNames.Length == 0
            ? $"Dynamic proxy rewrite using {context.Kind}."
            : $"Dynamic proxy rewrite using {context.Kind} with {string.Join(", ", interceptorDisplayNames)}.";

        return new DependencyInjectionDescriptorRewriteInfo
        {
            SourceModule = nameof(ModuleDynamicProxy),
            Summary = summary,
            RewriteKind = "DynamicProxy",
            ProxyKind = context.Kind.ToString(),
            RegistrationStyle = context.Way.ToString(),
            ImplementationType = context.ImplementType.GetCleanFullName(),
            ImplementationTypeDisplayName = context.ImplementType.GetCleanName(),
            ImplementationAssemblyName = context.ImplementType.Assembly.GetName().Name,
            ShouldInjectCachedServiceProvider = context.ShouldInjectCachedServiceProvider,
            InterceptorTypes = interceptorTypes
                .Select(type => type.GetCleanFullName())
                .ToArray(),
            InterceptorTypeDisplayNames = interceptorDisplayNames
        };
    }
}

/// <summary>
/// Enumeration of dynamic proxy kinds.
/// </summary>
public enum EDynamicProxyKind
{
    /// <summary>
    /// Creates proxies for virtual members on concrete classes. Currently only virtual methods are
    /// supported.
    /// </summary>
    ClassProxy,
    /// <summary>
    /// Proxy only the methods of implemented interfaces. Applicable only when the service type is an interface.
    /// </summary>
    InterfaceProxy,
}
