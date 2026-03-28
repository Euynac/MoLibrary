using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Authority.Identity.Models;
using Monica.SignalR.Interfaces;
using Monica.SignalR.Models;
using Monica.Modules;
using Monica.Core.Results;
using SignalRSwaggerGen.Attributes;

namespace Monica.SignalR.Services;

/// <summary>
/// SignalR business service implements core business logic such as Hub information acquisition and connection management
/// </summary>
/// <remarks>
/// Constructor
/// </remarks>
/// <param name="logger">Logger</param>
/// <param name="signalROptions">SignalR module options</param>
/// <param name="connectionManager">SignalR connection manager</param>
public class MoSignalRManageService(
    ILogger<MoSignalRManageService> logger,
    IOptions<ModuleSignalROption> signalROptions,
    IMoSignalRConnectionManager connectionManager)
{
    private readonly ModuleSignalROption _signalROption = signalROptions.Value;

    /// <summary>
    /// Get all server-side Hub information of SignalR
    /// </summary>
    /// <returns>SignalR server Hub information list</returns>
    public async Task<Res<List<SignalRServerGroupInfo>>> GetHubInfosAsync()
    {
        try
        {
            logger.LogInformation("开始获取SignalR Hub信息");

            var groups = _signalROption.Hubs
                .Select(hubInfo => new SignalRServerGroupInfo
                {
                    Source = hubInfo.HubType.Name,
                    Route = hubInfo.HubRoute,
                    Methods = hubInfo.HubType.GetMethods()
                        .Where(p => p.DeclaringType == hubInfo.HubType)
                        .Select(p => new SignalRServerMethodInfo
                        {
                            Desc = p.GetCustomAttribute<SignalRMethodAttribute>()?.Description ?? p.Name,
                            Name = p.Name,
                            Args = p.GetParameters().Select(a => new SignalRMethodParameter
                            {
                                Type = a.ParameterType.Name,
                                Name = a.Name ?? string.Empty
                            }).ToList()
                        }).ToList()
                }).ToList();

            logger.LogInformation("成功获取到 {HubCount} 个Hub信息", groups.Count);
            return await Task.FromResult(Res.Ok(groups));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取SignalR Hub信息失败");
            return Res.Fail($"获取Hub信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all currently connected SignalR users
    /// </summary>
    /// <returns>Connected user information list</returns>
    public async Task<Res<List<SignalRConnectedUserInfo>>> GetConnectedUsersAsync()
    {
        try
        {
            logger.LogInformation("开始获取SignalR已连接用户信息");

            var connections = connectionManager.GetConnectionInfos();
            var userInfos = connections.Select(conn => new SignalRConnectedUserInfo
            {
                ConnectionId = conn.ConnectionId,
                ConnectionTime = conn.ConnectionTime,
                IsAuthenticated = conn.ClaimsPrincipal.Identity?.IsAuthenticated ?? false,
                UserName = conn.ClaimsPrincipal.Identity?.Name,
                UserId = conn.ClaimsPrincipal.FindFirst(AuthorityClaimTypes.UserId)?.Value,
                Claims = conn.ClaimsPrincipal.Claims.ToDictionary(
                    c => c.Type,
                    c => c.Value
                )
            }).ToList();

            logger.LogInformation("成功获取到 {UserCount} 个已连接用户", userInfos.Count);
            return await Task.FromResult(Res.Ok(userInfos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取SignalR已连接用户信息失败");
            return Res.Fail($"获取已连接用户信息失败: {ex.Message}");
        }
    }
}
