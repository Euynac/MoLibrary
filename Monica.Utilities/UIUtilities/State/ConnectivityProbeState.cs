using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.Modules;
using Monica.Utilities.Connectivity.Facades;
using Monica.Utilities.Connectivity.Models;

namespace Monica.Utilities.UIUtilities.State;

/// <summary>
/// Owns the transient UI state for the connectivity probe panel.
/// </summary>
public sealed class ConnectivityProbeState(
    ConnectivityProbeFacade connectivityProbeFacade,
    IOptions<ModuleUtilitiesOption> moduleOptions)
{
    private readonly ModuleUtilitiesOption _moduleOption = moduleOptions.Value;

    /// <summary>
    /// Gets or sets the target host name or IP address.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target port. The protocol default is used when this value is left empty for HTTP or HTTPS probes.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the selected probe kind.
    /// </summary>
    public ConnectivityProbeKind ProbeKind { get; private set; } = ConnectivityProbeKind.Ping;

    /// <summary>
    /// Gets or sets the HTTP request path used by HTTP or HTTPS probes.
    /// </summary>
    public string Path { get; set; } = moduleOptions.Value.DefaultHttpRequestPath;

    /// <summary>
    /// Gets or sets the timeout applied to the next probe, in milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = moduleOptions.Value.DefaultProbeTimeoutMilliseconds;

    /// <summary>
    /// Gets or sets whether HTTPS certificate validation should be bypassed.
    /// </summary>
    public bool AllowInvalidCertificate { get; set; }

    /// <summary>
    /// Gets whether the panel is currently running a probe.
    /// </summary>
    public bool IsBusy { get; private set; }

    /// <summary>
    /// Gets the latest validation or execution error reported by the facade.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the latest completed probe result.
    /// </summary>
    public ConnectivityProbeResult? LastResult { get; private set; }

    /// <summary>
    /// Gets whether the current probe kind requires a target port.
    /// </summary>
    public bool RequiresPort => ProbeKind != ConnectivityProbeKind.Ping;

    /// <summary>
    /// Gets whether the current probe kind uses an HTTP request path.
    /// </summary>
    public bool RequiresHttpPath => ProbeKind is ConnectivityProbeKind.Http or ConnectivityProbeKind.Https;

    /// <summary>
    /// Gets whether the HTTPS certificate toggle should be visible.
    /// </summary>
    public bool ShowsCertificateToggle => ProbeKind == ConnectivityProbeKind.Https;

    /// <summary>
    /// Updates the selected probe kind and applies sensible default values for protocol-specific fields.
    /// </summary>
    /// <param name="probeKind">The new probe kind.</param>
    public void SetProbeKind(ConnectivityProbeKind probeKind)
    {
        ProbeKind = probeKind;

        if (probeKind == ConnectivityProbeKind.Ping)
        {
            Port = null;
            AllowInvalidCertificate = false;
            return;
        }

        switch (probeKind)
        {
            case ConnectivityProbeKind.Http when Port is null:
                Port = 80;
                break;
            case ConnectivityProbeKind.Https when Port is null:
                Port = 443;
                break;
        }

        if (!RequiresHttpPath)
        {
            if (probeKind != ConnectivityProbeKind.Https)
            {
                AllowInvalidCertificate = false;
            }

            return;
        }

        Path = string.IsNullOrWhiteSpace(Path) ? _moduleOption.DefaultHttpRequestPath : Path.Trim();
        if (probeKind != ConnectivityProbeKind.Https)
        {
            AllowInvalidCertificate = false;
        }
    }

    /// <summary>
    /// Runs the current probe request through the facade.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token that aborts the probe.</param>
    /// <returns>The result envelope returned by the facade.</returns>
    public async Task<Res<ConnectivityProbeResult>> RunAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await connectivityProbeFacade.ProbeAsync(
                new ConnectivityProbeRequest
                {
                    Host = Host,
                    Port = Port,
                    ProbeKind = ProbeKind,
                    Path = Path,
                    TimeoutMilliseconds = TimeoutMilliseconds,
                    AllowInvalidCertificate = AllowInvalidCertificate
                },
                cancellationToken);

            if (result.IsFailed(out var error, out var probeResult))
            {
                ErrorMessage = error.Message;
                LastResult = null;
            }
            else
            {
                LastResult = probeResult;
            }

            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Clears the current inputs and discards the previous result.
    /// </summary>
    public void Reset()
    {
        Host = string.Empty;
        Port = null;
        ProbeKind = ConnectivityProbeKind.Ping;
        Path = _moduleOption.DefaultHttpRequestPath;
        TimeoutMilliseconds = _moduleOption.DefaultProbeTimeoutMilliseconds;
        AllowInvalidCertificate = false;
        ErrorMessage = null;
        LastResult = null;
    }
}
