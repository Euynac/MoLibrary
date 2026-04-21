using System.Net;

namespace Monica.Utilities.Connectivity.Models;

/// <summary>
/// Identifies the transport or application-level probe that should be executed.
/// </summary>
public enum ConnectivityProbeKind
{
    /// <summary>
    /// Resolve the target and send an ICMP echo request.
    /// </summary>
    Ping,

    /// <summary>
    /// Resolve the target and attempt a raw TCP connection to the selected port.
    /// </summary>
    Tcp,

    /// <summary>
    /// Resolve the target, perform a TCP connection, then issue an HTTP request.
    /// </summary>
    Http,

    /// <summary>
    /// Resolve the target, perform a TCP connection, then issue an HTTPS request.
    /// </summary>
    Https
}

/// <summary>
/// Describes a connectivity probe request initiated by the UI or another caller.
/// </summary>
public sealed record ConnectivityProbeRequest
{
    /// <summary>
    /// Gets or sets the target host name or IP address to probe.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional target port.
    /// For HTTP and HTTPS probes the default protocol port is used when this value is omitted.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Gets or sets the requested probe kind.
    /// </summary>
    public ConnectivityProbeKind ProbeKind { get; init; } = ConnectivityProbeKind.Ping;

    /// <summary>
    /// Gets or sets the request path used by HTTP and HTTPS probes.
    /// </summary>
    public string Path { get; init; } = "/";

    /// <summary>
    /// Gets or sets the timeout applied to the probe, in milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; init; } = 3000;

    /// <summary>
    /// Gets or sets whether HTTPS certificate validation should be bypassed.
    /// </summary>
    public bool AllowInvalidCertificate { get; init; }

    /// <summary>
    /// Returns a normalized copy of the request with validated host, timeout, port, and HTTP path values.
    /// </summary>
    /// <param name="defaultTimeoutMilliseconds">Default timeout applied when the current request does not specify a positive timeout.</param>
    /// <param name="maximumTimeoutMilliseconds">Upper bound accepted by the module.</param>
    /// <param name="defaultHttpRequestPath">Fallback HTTP path applied when the request path is empty.</param>
    /// <returns>A normalized request that is safe for service execution.</returns>
    public ConnectivityProbeRequest Normalize(
        int defaultTimeoutMilliseconds,
        int maximumTimeoutMilliseconds,
        string defaultHttpRequestPath)
    {
        var host = Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("A host name or IP address is required.", nameof(Host));
        }

        var timeout = TimeoutMilliseconds > 0
            ? TimeoutMilliseconds
            : defaultTimeoutMilliseconds;
        timeout = Math.Clamp(timeout, 250, Math.Max(250, maximumTimeoutMilliseconds));

        var path = ProbeKind is ConnectivityProbeKind.Http or ConnectivityProbeKind.Https
            ? NormalizePath(string.IsNullOrWhiteSpace(Path) ? defaultHttpRequestPath : Path)
            : "/";

        var port = ProbeKind == ConnectivityProbeKind.Ping
            ? null
            : Port ?? ProbeKind switch
        {
            ConnectivityProbeKind.Http => 80,
            ConnectivityProbeKind.Https => 443,
            _ => null
        };

        if (ProbeKind != ConnectivityProbeKind.Ping && port is not (>= 1 and <= 65535))
        {
            throw new ArgumentException("A valid target port between 1 and 65535 is required.", nameof(Port));
        }

        return this with
        {
            Host = host,
            Port = port,
            Path = path,
            TimeoutMilliseconds = timeout,
            AllowInvalidCertificate = ProbeKind == ConnectivityProbeKind.Https && AllowInvalidCertificate
        };
    }

    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
    }
}

/// <summary>
/// Represents the aggregated result of a connectivity probe.
/// </summary>
public sealed record ConnectivityProbeResult(
    string Host,
    string Endpoint,
    ConnectivityProbeKind ProbeKind,
    int? Port,
    int TimeoutMilliseconds,
    DateTimeOffset StartedAt,
    long TotalDurationMilliseconds,
    bool Succeeded,
    IReadOnlyList<string> ResolvedAddresses,
    string? ResolutionError,
    ConnectivityPingProbeResult? Ping,
    ConnectivityTcpProbeResult? Tcp,
    ConnectivityHttpProbeResult? Http)
{
    private string HostWithPort => Port is int port ? $"{Host}:{port}" : Host;

    /// <summary>
    /// Human-readable summary describing the most important outcome for the probe.
    /// </summary>
    public string Summary =>
        ProbeKind switch
        {
            ConnectivityProbeKind.Ping when Succeeded =>
                $"Ping to {Host} replied from {Ping?.ReplyAddress ?? Host} in {Ping?.LatencyMilliseconds ?? TotalDurationMilliseconds} ms.",
            ConnectivityProbeKind.Ping =>
                $"Ping to {Host} failed: {Ping?.FailureMessage ?? Ping?.Status ?? ResolutionError ?? "Unknown error"}.",
            ConnectivityProbeKind.Tcp when Succeeded =>
                $"TCP connection to {HostWithPort} succeeded in {Tcp?.LatencyMilliseconds ?? TotalDurationMilliseconds} ms.",
            ConnectivityProbeKind.Tcp =>
                $"TCP connection to {HostWithPort} failed: {Tcp?.FailureMessage ?? ResolutionError ?? "Unknown error"}.",
            _ when Succeeded =>
                $"HTTP probe to {Endpoint} returned {(Http?.StatusCode?.ToString() ?? "response")} {Http?.ReasonPhrase}".TrimEnd() +
                $" in {Http?.LatencyMilliseconds ?? TotalDurationMilliseconds} ms.",
            _ =>
                $"HTTP probe to {Endpoint} failed: {Http?.FailureMessage ?? Tcp?.FailureMessage ?? ResolutionError ?? "Unknown error"}."
        };
}

/// <summary>
/// Captures the outcome of the ICMP echo request.
/// </summary>
public sealed record ConnectivityPingProbeResult(
    bool Succeeded,
    long? LatencyMilliseconds,
    string? ReplyAddress,
    string? Status,
    string? FailureMessage);

/// <summary>
/// Captures the outcome of the raw TCP connection attempt.
/// </summary>
public sealed record ConnectivityTcpProbeResult(
    bool Succeeded,
    long? LatencyMilliseconds,
    string? RemoteEndpoint,
    string? FailureMessage);

/// <summary>
/// Captures the outcome of the optional HTTP or HTTPS request.
/// Any received HTTP response is considered a successful application probe, even when the status code is not 2xx.
/// </summary>
public sealed record ConnectivityHttpProbeResult(
    bool Succeeded,
    string Method,
    string RequestUri,
    HttpStatusCode? StatusCode,
    string? ReasonPhrase,
    string? ServerHeader,
    long? LatencyMilliseconds,
    string? FailureMessage);
