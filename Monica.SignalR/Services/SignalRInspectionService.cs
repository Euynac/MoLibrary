using Microsoft.Extensions.Options;
using Monica.Authority.Identity.Models;
using Monica.Modules;
using Monica.SignalR.Abstractions;
using Monica.SignalR.Metrics;
using Monica.SignalR.Models;
using Monica.SignalR.Services.Support;

namespace Monica.SignalR.Services;

/// <summary>
/// Internal SignalR inspection service that exposes hub metadata and live connection snapshots.
/// </summary>
internal sealed class SignalRInspectionService(
    IOptions<ModuleSignalROption> signalROptions,
    SignalRHubMetadataReader hubMetadataReader,
    ISignalRConnectionRegistry connectionRegistry,
    SignalRSendMetrics sendMetrics)
{
    private readonly ModuleSignalROption _signalROption = signalROptions.Value;

    /// <summary>
    /// Reads metadata for all mapped hubs.
    /// </summary>
    public IReadOnlyList<SignalRHubInfo> GetHubInfos()
    {
        return hubMetadataReader.ReadHubMetadata(_signalROption.HubRegistrations);
    }

    /// <summary>
    /// Returns a snapshot of all connected SignalR users.
    /// </summary>
    public IReadOnlyList<SignalRConnectedUserInfo> GetConnectedUsers()
    {
        return connectionRegistry
            .GetConnectionInfos()
            .Select(connectionInfo => new SignalRConnectedUserInfo
            {
                ConnectionId = connectionInfo.ConnectionId,
                ConnectionTime = connectionInfo.ConnectionTime,
                IsAuthenticated = connectionInfo.ClaimsPrincipal.Identity?.IsAuthenticated ?? false,
                UserName = connectionInfo.ClaimsPrincipal.Identity?.Name,
                UserId = connectionInfo.ClaimsPrincipal.FindFirst(AuthorityClaimTypes.UserId)?.Value,
                Claims = connectionInfo.ClaimsPrincipal.Claims
                    .GroupBy(claim => claim.Type, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => string.Join(", ", group.Select(claim => claim.Value).Distinct(StringComparer.Ordinal)),
                        StringComparer.Ordinal)
            })
            .ToList();
    }

    /// <summary>
    /// Returns a snapshot of Monica-observed SignalR server-to-client send activity.
    /// </summary>
    public SignalRSendDiagnosticsSnapshot GetSendDiagnostics()
    {
        return sendMetrics.GetSnapshot();
    }
}
