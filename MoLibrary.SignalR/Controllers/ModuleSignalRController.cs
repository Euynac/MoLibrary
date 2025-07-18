using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.SignalR.Implements;
using MoLibrary.SignalR.Models;
using MoLibrary.SignalR.Modules;
using MoLibrary.Tool.MoResponse;
using SignalRSwaggerGen.Attributes;

namespace MoLibrary.SignalR.Controllers;
/// <summary>
/// SignalR业务服务，实现Hub信息获取等核心业务逻辑
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
/// <param name="logger">日志记录器</param>
/// <param name="signalROptions">SignalR模块选项</param>
public class MoSignalRManageService(ILogger<MoSignalRManageService> logger, IOptions<ModuleSignalROption> signalROptions)
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
}
/// <summary>
/// SignalR相关功能Controller
/// </summary>
[ApiController]
public class ModuleSignalRController(MoSignalRManageService service) : MoModuleControllerBase
{
    /// <summary>
    /// 获取SignalR所有Server端事件定义
    /// </summary>
    /// <returns>SignalR服务端方法信息列表</returns>
    [HttpGet("hubs")]
    public async Task<IActionResult> GetServerMethods()
    {
        return await service.GetHubInfosAsync().GetResponse(this);
    }
} 