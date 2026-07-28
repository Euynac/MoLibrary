using System.Reflection;

namespace Monica.DependencyInjection.DynamicProxy.Models;

/// <summary>
/// Describes the invocation-specific input exposed by the DynamicProxy execution bridge.
/// </summary>
/// <param name="Method">The concrete method being invoked.</param>
/// <param name="Arguments">A snapshot of the method arguments in declaration order.</param>
/// <param name="GenericArguments">The generic arguments supplied to the invocation.</param>
public sealed record DynamicProxyMethodInput(
    MethodInfo Method,
    IReadOnlyList<object?> Arguments,
    IReadOnlyList<Type> GenericArguments);
