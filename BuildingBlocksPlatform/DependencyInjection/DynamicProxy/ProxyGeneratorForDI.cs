using System.Text;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
namespace BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
/// <summary>
/// A proxy generator that integrates with Dependency Injection (DI) to create class proxies.
/// </summary>
[CLSCompliant(true)]
public class ProxyGeneratorWithDI : ProxyGenerator
{
    /// <summary>
    /// Creates a class proxy with Dependency Injection (DI).
    /// </summary>
    /// <param name="provider">The service provider for resolving dependencies.</param>
    /// <param name="classToProxy">The type of the class to proxy.</param>
    /// <param name="additionalInterfacesToProxy">Optional additional interfaces to proxy.</param>
    /// <param name="options">Proxy generation options.</param>
    /// <param name="constructorArguments">Optional constructor arguments for the proxied class.</param>
    /// <param name="interceptors">Interceptors to apply to the proxy.</param>
    /// <returns>A proxy instance of the specified class.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="classToProxy"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="classToProxy"/> is not a class.</exception>
    public object CreateClassProxyAndDI(IServiceProvider provider, Type classToProxy, Type[]? additionalInterfacesToProxy, ProxyGenerationOptions options,
        object[]? constructorArguments, params IInterceptor[] interceptors)
    {
        ArgumentNullException.ThrowIfNull(classToProxy);
        ArgumentNullException.ThrowIfNull(options);
        if (!classToProxy.IsClass)
            throw new ArgumentException("'classToProxy' must be a class", nameof(classToProxy));
        CheckNotGenericTypeDefinition(classToProxy, nameof(classToProxy));
        CheckNotGenericTypeDefinitions(additionalInterfacesToProxy, nameof(additionalInterfacesToProxy));
        var classProxyType = CreateClassProxyType(classToProxy, additionalInterfacesToProxy, options);
        var proxyArguments = BuildArgumentListForClassProxy(options, interceptors);
        if (constructorArguments != null && constructorArguments.Length != 0)
            proxyArguments.AddRange(constructorArguments);
        return CreateClassProxyInstanceWithDI(provider, classProxyType, proxyArguments, classToProxy, constructorArguments);
    }
    /// <summary>
    /// Creates a class proxy with a target instance and Dependency Injection (DI).
    /// </summary>
    /// <param name="provider">The service provider for resolving dependencies.</param>
    /// <param name="classToProxy">The type of the class to proxy.</param>
    /// <param name="additionalInterfacesToProxy">Optional additional interfaces to proxy.</param>
    /// <param name="target">The target instance to proxy.</param>
    /// <param name="options">Proxy generation options.</param>
    /// <param name="constructorArguments">Optional constructor arguments for the proxied class.</param>
    /// <param name="interceptors">Interceptors to apply to the proxy.</param>
    /// <returns>A proxy instance of the specified class with the target instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="classToProxy"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="classToProxy"/> is not a class.</exception>
    public object CreateClassProxyWithTargetAndDI(IServiceProvider provider, Type classToProxy, Type[]? additionalInterfacesToProxy, object target,
        ProxyGenerationOptions options, object[]? constructorArguments, params IInterceptor[] interceptors)
    {
        ArgumentNullException.ThrowIfNull(classToProxy);
        ArgumentNullException.ThrowIfNull(options);
        if (!classToProxy.IsClass)
            throw new ArgumentException("'classToProxy' must be a class", nameof(classToProxy));
        CheckNotGenericTypeDefinition(classToProxy, nameof(classToProxy));
        CheckNotGenericTypeDefinitions(additionalInterfacesToProxy, nameof(additionalInterfacesToProxy));
        var proxyTypeWithTarget = CreateClassProxyTypeWithTarget(classToProxy, additionalInterfacesToProxy, options);
        var proxyArguments = BuildArgumentListForClassProxyWithTarget(target, options, interceptors);
        if (constructorArguments != null && constructorArguments.Length != 0)
            proxyArguments.AddRange(constructorArguments);
        return CreateClassProxyInstanceWithDI(provider, proxyTypeWithTarget, proxyArguments, classToProxy, constructorArguments);
    }
    /// <summary>
    /// Creates an instance of a class proxy with Dependency Injection (DI).
    /// </summary>
    /// <param name="provider">The service provider for resolving dependencies.</param>
    /// <param name="proxyType">The type of the proxy to create.</param>
    /// <param name="proxyArguments">Arguments for the proxy constructor.</param>
    /// <param name="classToProxy">The type of the class being proxied.</param>
    /// <param name="constructorArguments">Optional constructor arguments for the proxied class.</param>
    /// <returns>An instance of the class proxy.</returns>
    /// <exception cref="ArgumentException">Thrown if the proxy cannot be instantiated.</exception>
    protected object CreateClassProxyInstanceWithDI(IServiceProvider provider, Type proxyType,
        List<object> proxyArguments,
        Type classToProxy,
        object?[]? constructorArguments)
    {
        try
        {
            return ActivatorUtilities.CreateInstance(provider, proxyType, [.. proxyArguments]);
        }
        catch (Exception ex)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendFormat("Can not instantiate proxy of class: {0}.", classToProxy.FullName);
            stringBuilder.AppendLine();
            if (constructorArguments == null || constructorArguments.Length == 0)
            {
                stringBuilder.Append("Could not find a parameterless constructor.");
            }
            else
            {
                stringBuilder.AppendLine("Could not find a constructor that would match given arguments:");
                foreach (var constructorArgument in constructorArguments)
                {
                    var str = constructorArgument == null ? "<null>" : constructorArgument.GetType().ToString();
                    stringBuilder.AppendLine(str);
                }
            }
            throw new ArgumentException(stringBuilder.ToString(), nameof(constructorArguments), ex);
        }
    }
}
