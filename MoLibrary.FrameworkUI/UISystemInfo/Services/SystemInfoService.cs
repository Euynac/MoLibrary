using MoLibrary.FrameworkUI.Modules;
using MoLibrary.FrameworkUI.UISystemInfo.Controllers;
using MoLibrary.FrameworkUI.UISystemInfo.Models;
using MoLibrary.UI.UICore.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UISystemInfo.Services;

/// <summary>
/// 系统信息服务，用于调用自身的Controller
/// </summary>
/// <param name="controllerInvoker">Controller调用器</param>
public class SystemInfoService(IUIControllerInvoker<ModuleSystemInfoUIOption> controllerInvoker)
{
    /// <summary>
    /// 获取系统信息
    /// </summary>
    /// <param name="simple">是否简化输出</param>
    /// <returns>系统信息</returns>
    public async Task<Res<SystemInfoResponse>> GetSystemInfoAsync(bool? simple = null)
    {
        // 构建API路径，包含查询参数
        var path = "system/info";
        if (simple.HasValue)
        {
            path += $"?simple={simple.Value}";
        }

        // 使用 IUIControllerInvoker 调用 Controller
        var result = await controllerInvoker.GetAsync<ModuleSystemInfoController, SystemInfoResponse>(path);

        // 直接返回 Res 结果，有错误时会自动传递
        return result;
    }

   
} 