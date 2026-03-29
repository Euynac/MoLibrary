using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.DependencyInjection.AppInterfaces;
using Monica.DependencyInjection.DynamicProxy.Abstract;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.DependencyInjection.DynamicProxy;

/// <summary>
/// Extension methods and classes for configuring and applying dynamic proxies in Microsoft Dependency Injection.
/// </summary>
public static class MicrosoftDependencyInjectionDynamicProxyExtensions
{
    /// <summary>
    /// Context for building a proxy.
    /// </summary>
    /// <param name="implementationType">The type of the implementation.</param>
    /// <param name="descriptor">The service descriptor.</param>
    public class ProxyBuildContext(Type implementationType, ServiceDescriptor descriptor)
    {
        public ServiceDescriptor ServiceDescriptor { get; set; } = descriptor;
        public Type ImplementationType { get; set; } = implementationType;
        public Type ServiceType => ServiceDescriptor.ServiceType;
    }

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
            collection.Add(new ServiceDescriptor(context.OldDescriptor.ServiceType, context.OldDescriptor.ServiceKey,
                (provider, o) =>
                {
                    var instance = context.OldDescriptor.ImplementationInstance!;
                    var proxyGenerator = provider.GetRequiredService<ProxyGeneratorWithDI>();
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
                }, context.OldDescriptor.Lifetime));
        }

        void AddFactoryRegister(RegisterContext context)
        {
            var factory = context.OldDescriptor.ImplementationFactory!;
            collection.Add(new ServiceDescriptor(context.OldDescriptor.ServiceType, context.OldDescriptor.ServiceKey,
                (provider, o) =>
                {
                    var proxyGenerator = provider.GetRequiredService<ProxyGeneratorWithDI>();
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
                }, context.OldDescriptor.Lifetime));
        }

        void AddNormalRegister(RegisterContext context)
        {
            // Important: Controllers must be added via AddControllersAsServices; otherwise, they
            // cannot be proxied dynamically.
            collection.Add(new ServiceDescriptor(context.OldDescriptor.ServiceType, context.OldDescriptor.ServiceKey,
                (provider, o) =>
                {
                    var proxyGenerator = provider.GetRequiredService<ProxyGeneratorWithDI>();
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
                }, context.OldDescriptor.Lifetime));
        }

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
