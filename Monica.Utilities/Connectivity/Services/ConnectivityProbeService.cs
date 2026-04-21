using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.Utilities.Connectivity.Models;

namespace Monica.Utilities.Connectivity.Services;

/// <summary>
/// Executes DNS, TCP, and optional HTTP or HTTPS probes for the utilities toolbox.
/// </summary>
public sealed class ConnectivityProbeService(IOptions<ModuleUtilitiesOption> options)
{
    private static readonly HttpMethod s_httpMethod = HttpMethod.Get;
    private readonly ModuleUtilitiesOption _option = options.Value;

    /// <summary>
    /// Runs the requested connectivity probe and returns a structured diagnostic result.
    /// </summary>
    /// <param name="request">Probe request supplied by the caller.</param>
    /// <param name="cancellationToken">Cancellation token that aborts the probe.</param>
    /// <returns>A structured result describing DNS, TCP, and optional HTTP outcomes.</returns>
    public async Task<ConnectivityProbeResult> ProbeAsync(
        ConnectivityProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = request.Normalize(
            _option.DefaultProbeTimeoutMilliseconds,
            _option.MaximumProbeTimeoutMilliseconds,
            _option.DefaultHttpRequestPath);

        var port = normalized.Port!.Value;
        var endpoint = BuildEndpoint(normalized, port);
        var startedAt = DateTimeOffset.UtcNow;
        var totalWatch = Stopwatch.StartNew();

        var (resolvedAddresses, resolutionError) = await ResolveAddressesAsync(normalized.Host, cancellationToken);
        var tcpResult = await ProbeTcpAsync(normalized.Host, port, normalized.TimeoutMilliseconds, cancellationToken);

        ConnectivityHttpProbeResult? httpResult = null;
        if (normalized.ProbeKind is ConnectivityProbeKind.Http or ConnectivityProbeKind.Https)
        {
            httpResult = tcpResult.Succeeded
                ? await ProbeHttpAsync(normalized, port, cancellationToken)
                : new ConnectivityHttpProbeResult(
                    false,
                    s_httpMethod.Method,
                    BuildUri(normalized, port).ToString(),
                    null,
                    null,
                    null,
                    null,
                    "HTTP request was skipped because the TCP handshake did not complete.");
        }

        totalWatch.Stop();

        var succeeded = normalized.ProbeKind switch
        {
            ConnectivityProbeKind.Tcp => tcpResult.Succeeded,
            _ => tcpResult.Succeeded && httpResult is { Succeeded: true }
        };

        return new ConnectivityProbeResult(
            normalized.Host,
            endpoint,
            normalized.ProbeKind,
            port,
            normalized.TimeoutMilliseconds,
            startedAt,
            totalWatch.ElapsedMilliseconds,
            succeeded,
            resolvedAddresses,
            resolutionError,
            tcpResult,
            httpResult);
    }

    private static async Task<(IReadOnlyList<string> Addresses, string? Error)> ResolveAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return ([ipAddress.ToString()], null);
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            var values = addresses
                .Select(p => p.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (values, null);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return ([], ex.Message);
        }
    }

    private static async Task<ConnectivityTcpProbeResult> ProbeTcpAsync(
        string host,
        int port,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMilliseconds);
        var watch = Stopwatch.StartNew();

        try
        {
            await tcpClient.ConnectAsync(host, port, timeoutCts.Token);
            watch.Stop();

            return new ConnectivityTcpProbeResult(
                true,
                watch.ElapsedMilliseconds,
                tcpClient.Client.RemoteEndPoint?.ToString(),
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            watch.Stop();
            return new ConnectivityTcpProbeResult(false, watch.ElapsedMilliseconds, null, $"TCP connection timed out after {timeoutMilliseconds} ms.");
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            watch.Stop();
            return new ConnectivityTcpProbeResult(false, watch.ElapsedMilliseconds, null, ex.Message);
        }
    }

    private static async Task<ConnectivityHttpProbeResult> ProbeHttpAsync(
        ConnectivityProbeRequest request,
        int port,
        CancellationToken cancellationToken)
    {
        var requestUri = BuildUri(request, port);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(request.TimeoutMilliseconds);

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(request.TimeoutMilliseconds),
            AllowAutoRedirect = false
        };

        if (request.ProbeKind == ConnectivityProbeKind.Https && request.AllowInvalidCertificate)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true
            };
        }

        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(request.TimeoutMilliseconds)
        };

        using var requestMessage = new HttpRequestMessage(s_httpMethod, requestUri);
        requestMessage.Headers.UserAgent.Add(new ProductInfoHeaderValue("Monica.Utilities", "0.1"));

        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            watch.Stop();

            return new ConnectivityHttpProbeResult(
                true,
                s_httpMethod.Method,
                requestUri.ToString(),
                response.StatusCode,
                response.ReasonPhrase,
                response.Headers.Server.ToString(),
                watch.ElapsedMilliseconds,
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            watch.Stop();
            return new ConnectivityHttpProbeResult(
                false,
                s_httpMethod.Method,
                requestUri.ToString(),
                null,
                null,
                null,
                watch.ElapsedMilliseconds,
                $"HTTP request timed out after {request.TimeoutMilliseconds} ms.");
        }
        catch (HttpRequestException ex)
        {
            watch.Stop();
            return new ConnectivityHttpProbeResult(
                false,
                s_httpMethod.Method,
                requestUri.ToString(),
                null,
                null,
                null,
                watch.ElapsedMilliseconds,
                ex.Message);
        }
    }

    private static string BuildEndpoint(ConnectivityProbeRequest request, int port)
    {
        return request.ProbeKind switch
        {
            ConnectivityProbeKind.Tcp => $"{request.Host}:{port}",
            _ => BuildUri(request, port).ToString()
        };
    }

    private static Uri BuildUri(ConnectivityProbeRequest request, int port)
    {
        return new UriBuilder
        {
            Scheme = request.ProbeKind == ConnectivityProbeKind.Https ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
            Host = request.Host,
            Port = port,
            Path = request.Path
        }.Uri;
    }
}
