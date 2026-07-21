namespace Monica.WebApi.Annotations;

/// <summary>
/// Declares the HTTP endpoint contract owned by an application request.
/// </summary>
/// <remarks>
/// Requests in a strict <c>PublishedLanguages.Domain{Domain}.Requests</c> namespace also define
/// an RPC operation. Requests declared elsewhere remain local HTTP contracts.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ApiEndpointAttribute(ApiHttpMethod method, string route) : Attribute
{
    /// <summary>
    /// Gets the HTTP method exposed by the generated controller and RPC client.
    /// </summary>
    public ApiHttpMethod Method { get; } = method;

    /// <summary>
    /// Gets the route relative to the configured route prefix and domain.
    /// </summary>
    public string Route { get; } = route;

    /// <summary>
    /// Gets or sets how the generated controller and client bind the request.
    /// <see cref="ApiRequestBinding.Auto"/> uses query binding for GET and DELETE and body binding
    /// for the other methods.
    /// </summary>
    public ApiRequestBinding Binding { get; set; } = ApiRequestBinding.Auto;

    /// <summary>
    /// Gets or sets the generated RPC method name. When omitted, the request's
    /// <c>Command</c> or <c>Query</c> prefix is removed.
    /// </summary>
    public string? OperationName { get; set; }
}
