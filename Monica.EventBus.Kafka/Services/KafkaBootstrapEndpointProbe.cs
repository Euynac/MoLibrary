using System.Globalization;
using System.Net.Sockets;
using Monica.EventBus.Kafka.Models;
using Microsoft.Extensions.Options;
using Monica.Modules;

namespace Monica.EventBus.Kafka.Services;

/// <summary>
/// Performs a managed TCP preflight against Kafka bootstrap endpoints before native clients are created.
/// </summary>
public sealed class KafkaBootstrapEndpointProbe(IOptions<ModuleEventBusKafkaOption> options)
{
    private const int DEFAULT_KAFKA_PORT = 9092;

    /// <summary>
    /// Tests whether at least one bootstrap endpoint accepts a TCP connection.
    /// </summary>
    /// <param name="cluster">Cluster configuration to probe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preflight result.</returns>
    public async Task<KafkaBootstrapEndpointProbeResult> ProbeAsync(
        KafkaClusterConfig cluster,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        IReadOnlyList<KafkaBootstrapEndpoint> endpoints;
        try
        {
            endpoints = ParseEndpoints(cluster.BootstrapServers);
        }
        catch (FormatException ex)
        {
            return KafkaBootstrapEndpointProbeResult.Fail(ex.Message);
        }

        if (endpoints.Count == 0)
        {
            return KafkaBootstrapEndpointProbeResult.Fail("Kafka bootstrap servers are not configured.");
        }

        var errors = new List<string>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            try
            {
                await ProbeEndpointAsync(endpoint, cancellationToken);
                return KafkaBootstrapEndpointProbeResult.Success(endpoint.DisplayName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{endpoint.DisplayName}: {ex.Message}");
            }
        }

        return KafkaBootstrapEndpointProbeResult.Fail(
            $"No Kafka bootstrap endpoint is reachable. {string.Join("; ", errors)}");
    }

    private async Task ProbeEndpointAsync(KafkaBootstrapEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(options.Value.BootstrapEndpointProbeTimeout);

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(endpoint.Host, endpoint.Port, timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("TCP connection timed out.");
        }
    }

    private static IReadOnlyList<KafkaBootstrapEndpoint> ParseEndpoints(string? bootstrapServers)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            return [];
        }

        return bootstrapServers
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseEndpoint)
            .ToList();
    }

    private static KafkaBootstrapEndpoint ParseEndpoint(string value)
    {
        if (value.StartsWith("[", StringComparison.Ordinal))
        {
            return ParseBracketedEndpoint(value);
        }

        var separatorIndex = value.LastIndexOf(':');
        if (separatorIndex < 0)
        {
            return new KafkaBootstrapEndpoint(value, DEFAULT_KAFKA_PORT);
        }

        var host = value[..separatorIndex].Trim();
        var portText = value[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new FormatException($"Kafka bootstrap endpoint '{value}' does not define a host.");
        }

        return new KafkaBootstrapEndpoint(host, ParsePort(value, portText));
    }

    private static KafkaBootstrapEndpoint ParseBracketedEndpoint(string value)
    {
        var endBracketIndex = value.IndexOf(']', StringComparison.Ordinal);
        if (endBracketIndex <= 1)
        {
            throw new FormatException($"Kafka bootstrap endpoint '{value}' is not a valid bracketed IPv6 endpoint.");
        }

        var host = value[1..endBracketIndex].Trim();
        var remainder = value[(endBracketIndex + 1)..].Trim();
        if (remainder.Length == 0)
        {
            return new KafkaBootstrapEndpoint(host, DEFAULT_KAFKA_PORT);
        }

        if (!remainder.StartsWith(":", StringComparison.Ordinal))
        {
            throw new FormatException($"Kafka bootstrap endpoint '{value}' has invalid text after the host.");
        }

        return new KafkaBootstrapEndpoint(host, ParsePort(value, remainder[1..].Trim()));
    }

    private static int ParsePort(string endpoint, string portText)
    {
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            port is <= 0 or > 65535)
        {
            throw new FormatException($"Kafka bootstrap endpoint '{endpoint}' does not define a valid TCP port.");
        }

        return port;
    }

    private sealed record KafkaBootstrapEndpoint(string Host, int Port)
    {
        public string DisplayName => $"{Host}:{Port}";
    }
}

/// <summary>
/// Result of a Kafka bootstrap endpoint preflight.
/// </summary>
public sealed class KafkaBootstrapEndpointProbeResult
{
    private KafkaBootstrapEndpointProbeResult(bool isReachable, string? endpoint, string? errorMessage)
    {
        IsReachable = isReachable;
        Endpoint = endpoint;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Whether at least one endpoint accepted a TCP connection.
    /// </summary>
    public bool IsReachable { get; }

    /// <summary>
    /// First endpoint that accepted a TCP connection, when available.
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// Failure details when no endpoint could be reached.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates a successful probe result.
    /// </summary>
    /// <param name="endpoint">Reachable endpoint.</param>
    /// <returns>A successful result.</returns>
    public static KafkaBootstrapEndpointProbeResult Success(string endpoint)
    {
        return new KafkaBootstrapEndpointProbeResult(true, endpoint, null);
    }

    /// <summary>
    /// Creates a failed probe result.
    /// </summary>
    /// <param name="errorMessage">Failure details.</param>
    /// <returns>A failed result.</returns>
    public static KafkaBootstrapEndpointProbeResult Fail(string errorMessage)
    {
        return new KafkaBootstrapEndpointProbeResult(false, null, errorMessage);
    }
}
