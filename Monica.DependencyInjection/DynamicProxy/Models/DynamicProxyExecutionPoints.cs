using Monica.Core.Execution;

namespace Monica.DependencyInjection.DynamicProxy.Models;

/// <summary>
/// Execution points published by the optional DynamicProxy compatibility bridge.
/// </summary>
public static class DynamicProxyExecutionPoints
{
    /// <summary>
    /// Identifies an asynchronous service method routed through a Monica dynamic proxy.
    /// </summary>
    public static ExecutionPoint Method { get; } = new("dynamic-proxy.method");
}
