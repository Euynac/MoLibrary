using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.SignalR.Models;
using MoLibrary.SignalR.Modules;
using MoLibrary.Tool.MoResponse;
using SignalRSwaggerGen.Attributes;

namespace MoLibrary.SignalR.Controllers;

/// <summary>
/// SignalR相关功能Controller
/// </summary>
[ApiController]
public class ModuleSignalRController(IOptions<ModuleSignalROption> options) : MoModuleControllerBase
{
    private readonly ModuleSignalROption _option = options.Value;

    /// <summary>
    /// 获取SignalR所有Server端事件定义
    /// </summary>
    /// <returns>SignalR服务端方法信息列表</returns>
    [HttpGet("/hubs")]
    public IActionResult GetServerMethods()
    {
        // 遍历所有注册的Hub类型，提取其所有方法，并增加 source 属性（类型名）
        var methods = _option.Hubs
            .SelectMany(hubType =>
                hubType.GetMethods()
                    .Where(p => p.DeclaringType == hubType)
                    .Select(p => new SignalRServerMethodInfo
                    {
                        Desc = p.GetCustomAttribute<SignalRMethodAttribute>()?.Description ?? p.Name,
                        Name = p.Name,
                        Args = p.GetParameters().Select(a => new SignalRMethodParameter
                        {
                            Type = a.ParameterType.Name,
                            Name = a.Name ?? string.Empty
                        }).ToList(),
                        Source = hubType.Name
                    })
            ).ToList();

        return Res.Ok(methods).GetResponse(this);
    }
} 