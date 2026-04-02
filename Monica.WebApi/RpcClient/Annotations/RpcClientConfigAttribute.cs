using Monica.WebApi.RpcClient.Abstractions;

namespace Monica.WebApi.RpcClient.Annotations;

/// <summary>
/// Configures generation settings for client-side API callers.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class RpcClientConfigAttribute : Attribute
{
    /// <summary>
    /// Whether to generate gRPC implementations.
    /// </summary>
    public bool AddGrpcImplementations { get; set; } = false;
    /// <summary>
    /// Whether to generate HTTP implementations.
    /// </summary>
    public bool AddHttpImplementations { get; set; } = true;

    /// <summary>
    /// HTTP implementation base type. Defaults to <see cref="HttpRpcApi"/> when not specified.
    /// This type is used as the generated base class and to resolve the required namespaces.
    /// Custom implementations must inherit from <see cref="HttpRpcApi"/> and must not introduce extra constructor parameters.
    /// </summary>
    public Type? HttpImplementationType { get; set; }
}
