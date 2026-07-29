using System.Reflection;

namespace Monica.DependencyInjection.DynamicProxy.Abstractions;

/// <summary>
/// Describes a method invocation flowing through a Monica dynamic proxy.
/// </summary>
public interface IMethodInvocation
{
    /// <summary>
    /// Gets the raw argument values in declaration order.
    /// </summary>
    object[] Arguments { get; }

    /// <summary>
    /// Gets the invocation arguments keyed by parameter name.
    /// </summary>
    IReadOnlyDictionary<string, object> ArgumentsDictionary { get; }

    /// <summary>
    /// Gets the generic type arguments supplied to the method call.
    /// </summary>
    Type[] GenericArguments { get; }

    /// <summary>
    /// Gets the concrete application component type selected when the proxy target is created.
    /// </summary>
    /// <remarks>
    /// This type is stable for inherited methods and class proxies; it is not the generated proxy type or the
    /// method's declaring base type. Factory registrations use the concrete type returned by the factory.
    /// </remarks>
    Type ComponentType { get; }

    /// <summary>
    /// Gets the target object that will receive the invocation.
    /// </summary>
    object TargetObject { get; }

    /// <summary>
    /// Gets the reflected method metadata for the invocation.
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// Gets or sets the return value captured by the invocation adapter.
    /// </summary>
    object ReturnValue { get; set; }

    /// <summary>
    /// Continues execution of the underlying target method.
    /// </summary>
    Task ProceedAsync();
}
