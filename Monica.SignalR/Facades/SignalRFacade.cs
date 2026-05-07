using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.SignalR.Models;
using Monica.SignalR.Services;

namespace Monica.SignalR.Facades;

/// <summary>
/// Public SignalR entry point used by Minimal API handlers and UI components.
/// </summary>
public sealed class SignalRFacade(
    ILogger<SignalRFacade> logger,
    IServiceProvider serviceProvider)
{
    /// <summary>
    /// Gets metadata for all registered SignalR hubs.
    /// </summary>
    public Task<Res<List<SignalRHubInfo>>> GetHubInfosAsync()
    {
        try
        {
            var inspectionService = serviceProvider.GetRequiredService<SignalRInspectionService>();
            return Task.FromResult(Res.Ok(inspectionService.GetHubInfos().ToList()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get SignalR hub metadata.");
            return Task.FromResult<Res<List<SignalRHubInfo>>>(Res.Fail(
                $"Failed to get SignalR hub metadata: {ex.GetMessageRecursively()}",
                ResStatus.InternalError));
        }
    }

    /// <summary>
    /// Gets the currently connected SignalR users.
    /// </summary>
    public Task<Res<List<SignalRConnectedUserInfo>>> GetConnectedUsersAsync()
    {
        try
        {
            var inspectionService = serviceProvider.GetRequiredService<SignalRInspectionService>();
            return Task.FromResult(Res.Ok(inspectionService.GetConnectedUsers().ToList()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get connected SignalR users.");
            return Task.FromResult<Res<List<SignalRConnectedUserInfo>>>(Res.Fail(
                $"Failed to get connected SignalR users: {ex.GetMessageRecursively()}",
                ResStatus.InternalError));
        }
    }

    /// <summary>
    /// Gets Monica-observed SignalR server-to-client send diagnostics.
    /// </summary>
    public Task<Res<SignalRSendDiagnosticsSnapshot>> GetSendDiagnosticsAsync()
    {
        try
        {
            var inspectionService = serviceProvider.GetRequiredService<SignalRInspectionService>();
            return Task.FromResult(Res.Ok(inspectionService.GetSendDiagnostics()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get SignalR send diagnostics.");
            return Task.FromResult<Res<SignalRSendDiagnosticsSnapshot>>(Res.Fail(
                $"Failed to get SignalR send diagnostics: {ex.GetMessageRecursively()}",
                ResStatus.InternalError));
        }
    }
}
