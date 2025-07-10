using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DependencyInjection.DynamicProxy.Abstract;
using MoLibrary.DependencyInjection.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DependencyInjection.DynamicProxy;

/// <summary>
/// Extension methods and classes for configuring and applying dynamic proxies in Microsoft Dependency Injection.
/// </summary>
public static class MicrosoftDependencyInjectionDynamicProxyExtensions
{
    /// <summary>
    /// Represents information about a proxy.
    /// </summary>
    /// <param name="judgeFunc">A function to determine if the proxy should be applied.</param>
    public class ProxyInfo(Func<ProxyBuildContext, bool> judgeFunc)
    {
        public Func<ProxyBuildContext, bool> JudgeFunc { get; set; } = judgeFunc;
    }

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
    /// Builder for configuring proxies.
    /// </summary>
    public class ProxyBuilder
    {
        public static List<ProxyBuilder> Builders { get; } = [];
        public List<Type> InterceptorTypes { get; } = [];
        public ProxyInfo? Info { get; private set; }

        /// <summary>
        /// Adds an interceptor to the proxy.
        /// </summary>
        /// <typeparam name="TInterceptor">The type of the interceptor.</typeparam>
        /// <returns>The proxy builder.</returns>
        public ProxyBuilder AddInterceptor<TInterceptor>() where TInterceptor : MoInterceptor
        {
            var interceptorAdapterType =
                typeof(MoAsyncDeterminationInterceptor<>).MakeGenericType(typeof(TInterceptor));
            AddInterceptor(interceptorAdapterType);
            return this;
        }

        internal void Build(ProxyInfo info)
        {
            Info = info;
            Builders.Add(this);
        }

        protected ProxyBuilder AddInterceptor(Type type)
        {
            InterceptorTypes.Add(type);
            return this;
        }
    }

    /// <summary>
    /// Adds an interceptor to the service collection.
    /// </summary>
    /// <typeparam name="TInterceptor">The type of the interceptor.</typeparam>
    /// <param name="serviceCollection">The service collection.</param>
    /// <returns>The proxy builder.</returns>
    public static ProxyBuilder AddMoInterceptor<TInterceptor>(this IServiceCollection serviceCollection)
        where TInterceptor : MoInterceptor
    {
        var builder = new ProxyBuilder();
        serviceCollection.AddTransient<TInterceptor>();
        builder.AddInterceptor<TInterceptor>();
        return builder;
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
        public RegisterContext(ModuleDynamicProxyOption option, bool shouldInjectServiceProvider,
            ServiceDescriptor oldDescriptor,
            Type implementType,
            List<Type> interceptorTypes, ERegisterWays way)
        {
            Option = option;
            ShouldInjectServiceProvider = shouldInjectServiceProvider;
            OldDescriptor = oldDescriptor;
            ImplementType = implementType;
            InterceptorTypes = interceptorTypes;
            Way = way;
            CalculateProxyKind();
            ValidateServiceInject();
        }

        public ModuleDynamicProxyOption Option { get; }
        public bool ShouldInjectServiceProvider { get; }
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

            foreach (var builder in ProxyBuilder.Builders)
            {
                if (builder.Info is null) continue;
                if (!builder.Info.JudgeFunc.Invoke(new ProxyBuildContext(implementType, oldDescriptor))) continue;
                interceptorTypes.AddRange(builder.InterceptorTypes);
            }
            if (interceptorTypes.Count <= 0) continue;

            collection.RemoveAt(index);

            var shouldInjectServiceProvider = implementType.IsImplementInterface<IMoServiceProviderInjector>();
            var context = new RegisterContext(option, shouldInjectServiceProvider, oldDescriptor, implementType, interceptorTypes, way);
           
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

        void InjectServiceProvider(object proxiedObject, IServiceProvider provider, RegisterContext context)
        {
            if (context.ShouldInjectServiceProvider)
            {
                ((IMoServiceProviderInjector) proxiedObject).MoProvider = provider.GetRequiredService<IMoServiceProvider>();
            }
        }

        //同种类Interceptor不应该注册多次？
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
                            InjectServiceProvider(proxiedObject, provider, context);
                            break;
                        case EDynamicProxyKind.InterfaceProxy:
                            InjectServiceProvider(instance, provider, context);
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
                    //TODO 无法实现属性注入，因为工厂方法实例化只能执行一次。
                    switch (context.Kind)
                    {
                        case EDynamicProxyKind.ClassProxy:
                            proxiedObject = proxyGenerator.CreateClassProxyWithTargetAndDI(provider,
                                context.ImplementType, null, targetFromFactory, new ProxyGenerationOptions(), null,
                                interceptors);

                            InjectServiceProvider(proxiedObject, provider, context);
                            break;
                        case EDynamicProxyKind.InterfaceProxy:

                            InjectServiceProvider(targetFromFactory, provider, context);
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
                            InjectServiceProvider(proxiedObject, provider, context);
                            break;
                        case EDynamicProxyKind.InterfaceProxy:
                            proxiedObject = ActivatorUtilities.CreateInstance(provider, context.ImplementType);
                            InjectServiceProvider(proxiedObject, provider, context); //run before create proxied instance because interface can not inject service provider.
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

    /// <summary>
    /// Configures the proxy to be created when the specified condition is satisfied.
    /// </summary>
    /// <param name="proxyBuilder">The proxy builder.</param>
    /// <param name="judgeFunc">The function to determine if the proxy should be created.</param>
    public static void CreateProxyWhenSatisfy(this ProxyBuilder proxyBuilder,
        Func<ProxyBuildContext, bool> judgeFunc)
    {
        proxyBuilder.Build(new ProxyInfo(judgeFunc));
    }
}

/// <summary>
/// Enumeration of dynamic proxy kinds.
/// </summary>
public enum EDynamicProxyKind
{
    /// <summary>
    /// Proxy class methods marked as virtual.   TODO 目前仅支持virtual方法的代理
    /// </summary>
    ClassProxy,
    /// <summary>
    /// Proxy only the methods of implemented interfaces. Applicable only when the service type is an interface.
    /// </summary>
    InterfaceProxy,
}
