using ModelContextProtocol.Client;

namespace Monica.AI.Mcp.Models;

/// <summary>
/// Serializable configuration for an external HTTP MCP client.
/// </summary>
public sealed record ExternalMcpClientProfile
{
    /// <summary>
    /// Stable client name shown to management UIs and used as the capability key.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Short description of the remote MCP server or connection purpose.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Absolute HTTP or HTTPS endpoint for the remote MCP server.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// HTTP transport mode used by the MCP SDK. Auto-detect tries Streamable HTTP first and falls back to SSE.
    /// </summary>
    public HttpTransportMode TransportMode { get; init; } = HttpTransportMode.AutoDetect;

    /// <summary>
    /// Timeout in seconds for establishing the MCP HTTP connection. Values less than one use the SDK default.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Additional HTTP headers sent to the remote MCP server. Values are stored as configured by the host.
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether listed client tools should be exposed to Monica agents.
    /// </summary>
    public bool IsAgentToolEnabled { get; init; } = true;

    /// <summary>
    /// Gets whether this profile can be edited from the runtime management UI.
    /// </summary>
    public bool IsUserManaged => Origin == ExternalMcpClientProfileOrigin.User;

    /// <summary>
    /// Origin of this profile. Code-defined profiles are read-only at runtime.
    /// </summary>
    public ExternalMcpClientProfileOrigin Origin { get; init; } = ExternalMcpClientProfileOrigin.User;

    /// <summary>
    /// Returns a normalized copy suitable for persistence or runtime use.
    /// </summary>
    public ExternalMcpClientProfile Normalize(ExternalMcpClientProfileOrigin origin)
    {
        return this with
        {
            Name = (Name ?? string.Empty).Trim(),
            Description = (Description ?? string.Empty).Trim(),
            Endpoint = (Endpoint ?? string.Empty).Trim(),
            ConnectionTimeoutSeconds = Math.Max(1, ConnectionTimeoutSeconds),
            Headers = NormalizeHeaders(Headers),
            Origin = origin
        };
    }

    /// <summary>
    /// Throws when the profile cannot be used to create an MCP HTTP client.
    /// </summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(Endpoint);

        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("External MCP endpoint must be an absolute HTTP or HTTPS URL.", nameof(Endpoint));
        }

        foreach (var header in Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                throw new ArgumentException("External MCP header names cannot be empty.", nameof(Headers));
            }

            if (header.Value is null)
            {
                throw new ArgumentException("External MCP header values cannot be null.", nameof(Headers));
            }
        }
    }

    private static Dictionary<string, string> NormalizeHeaders(IDictionary<string, string>? headers)
    {
        return (headers ?? new Dictionary<string, string>())
            .Where(static header => !string.IsNullOrWhiteSpace(header.Key))
            .Select(static header => new KeyValuePair<string, string>(header.Key.Trim(), header.Value?.Trim() ?? string.Empty))
            .GroupBy(static header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Last().Value,
                StringComparer.OrdinalIgnoreCase);
    }
}
