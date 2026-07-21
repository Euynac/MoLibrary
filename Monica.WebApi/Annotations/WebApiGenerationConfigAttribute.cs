using Monica.WebApi.RpcClient.Abstractions;

namespace Monica.WebApi.Annotations;

/// <summary>
/// Configures request-owned controller and RPC source generation for an assembly.
/// </summary>
/// <param name="routePrefix">
/// The route prefix placed before the domain and request route, such as <c>api/v1</c>.
/// Leading and trailing slashes are not permitted.
/// </param>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class WebApiGenerationConfigAttribute(string routePrefix) : Attribute
{
    /// <summary>
    /// Gets the route prefix placed before the domain and request route.
    /// </summary>
    public string RoutePrefix { get; } = routePrefix;

    /// <summary>
    /// Gets or sets the domain used by local request contracts. For published requests, this value,
    /// when supplied, must match the domain derived from the published-language namespace.
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Gets or sets the transport implementations generated for published RPC contracts.
    /// The RPC interfaces are generated even when this value is <see cref="RpcClientGenerationTargets.None"/>.
    /// </summary>
    public RpcClientGenerationTargets RpcClientTargets { get; set; }

    /// <summary>
    /// Gets or sets the base type for generated HTTP clients. The default is <see cref="HttpRpcApi"/>.
    /// A custom type must inherit <see cref="HttpRpcApi"/> and expose the same constructor dependencies.
    /// </summary>
    public Type? HttpClientBaseType { get; set; }

    /// <summary>
    /// Gets or sets the base type for generated local clients. The default is <see cref="LocalRpcApi"/>.
    /// A custom type must inherit <see cref="LocalRpcApi"/> and expose the same constructor dependencies.
    /// </summary>
    public Type? LocalClientBaseType { get; set; }
}
