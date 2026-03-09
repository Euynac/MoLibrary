using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Authority.Security;
using Monica.SignalR.Interfaces;
using Monica.SignalR.Models;
using Monica.Modules;
using Monica.Tool.MoResponse;
using SignalRSwaggerGen.Attributes;

namespace Monica.SignalR.Services;

/// <summary>
/// SignalR业务服务，实现Hub信息获取和连接管理等核心业务逻辑
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="logger">日志记录器</param>
/// <param name="signalROptions">SignalR模块选项</param>
/// <param name="connectionManager">SignalR连接管理器</param>
public class MoSignalRManageService(
    ILogger<MoSignalRManageService> logger,
    IOptions<ModuleSignalROption> signalROptions,
    IMoSignalRConnectionManager connectionManager)
{
    private readonly ModuleSignalROption _signalROption = signalROptions.Value;

    /// <summary>
    /// 获取SignalR所有Server端Hub信息
    /// </summary>
    /// <returns>SignalR服务端Hub信息列表</returns>
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
    /// 获取当前所有已连接的SignalR用户
    /// </summary>
    /// <returns>已连接用户信息列表</returns>
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
                UserId = conn.ClaimsPrincipal.FindFirst(MoClaimTypes.UserId)?.Value,
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
