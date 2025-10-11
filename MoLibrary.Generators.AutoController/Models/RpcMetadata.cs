using System.Collections.Generic;

namespace MoLibrary.Generators.AutoController.Models;

/// <summary>
/// Represents metadata for RPC client generation.
/// This class is serialized to JSON and consumed by the RPC client source generator.
/// </summary>
internal class RpcMetadata
{
    /// <summary>
    /// The name of the assembly containing the handlers.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// The domain name associated with this assembly (e.g., "Flight", "Message").
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// The default route prefix for handlers in this assembly (e.g., "api/v1").
    /// </summary>
    public string? RoutePrefix { get; set; }

    /// <summary>
    /// Collection of unique namespaces from all request and response types used by handlers in this assembly.
    /// These namespaces are used to generate appropriate 'using' statements in client code.
    /// </summary>
    public List<string> RelatedNamespaces { get; set; } = new();

    /// <summary>
    /// Collection of handler metadata for RPC client generation.
    /// </summary>
    public List<HandlerMetadata> Handlers { get; set; } = new();
}

/// <summary>
/// Represents metadata for a single handler method.
/// </summary>
internal class HandlerMetadata
{
    /// <summary>
    /// The fully qualified type name of the request parameter.
    /// </summary>
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// The fully qualified type name of the response type.
    /// </summary>
    public string ResponseType { get; set; } = string.Empty;

    /// <summary>
    /// The HTTP method (e.g., "POST", "GET").
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// The complete route for this handler (e.g., "api/v1/Flight/transport-flights").
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// The client method name to be generated (e.g., "GetFlightListToDay").
    /// </summary>
    public string ClientMethodName { get; set; } = string.Empty;

    /// <summary>
    /// The handler type (e.g., "Command", "Query").
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// The plain text summary documentation for this handler (without XML tags).
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}
