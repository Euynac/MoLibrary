using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MoLibrary.FrameworkUI.Modules;
using MoLibrary.FrameworkUI.UISystemInfo.Controllers;
using MoLibrary.FrameworkUI.UISystemInfo.Models;
using MoLibrary.Tool.MoResponse;
using System.Text.Json;
using MoLibrary.UI.UICore;

namespace MoLibrary.FrameworkUI.UISystemInfo.Services;

/// <summary>
/// 系统信息服务，用于调用自身的Controller
/// 注意：这是原始实现，新的实现应该使用 IMoUIControllerInvoker&lt;ModuleSystemInfoUIOption&gt;
/// </summary>
public class SystemInfoService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly ModuleSystemInfoUIOption _option;
    
    // 新的抽象调用器（可选使用）
    private readonly IMoUIControllerInvoker<ModuleSystemInfoUIOption>? _controllerInvoker;

    public SystemInfoService(HttpClient httpClient, NavigationManager navigationManager, IOptions<ModuleSystemInfoUIOption> option)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
        _option = option.Value;
    }
    
    /// <summary>
    /// 使用新抽象的构造函数（推荐）
    /// </summary>
    /// <param name="controllerInvoker">Controller调用器</param>
    public SystemInfoService(IMoUIControllerInvoker<ModuleSystemInfoUIOption> controllerInvoker)
    {
        _controllerInvoker = controllerInvoker;
    }

    /// <summary>
    /// 获取系统信息
    /// </summary>
    /// <param name="simple">是否简化输出</param>
    /// <returns>系统信息</returns>
    public async Task<SystemInfoResponse?> GetSystemInfoAsync(bool? simple = null)
    {
        try
        {
            // 使用Option的GetRoute方法构建API URL
            var controllerRoute = _option.GetRoute<ModuleSystemInfoController>("system/info");
            var baseUri = _navigationManager.BaseUri.TrimEnd('/');
            var url = $"{baseUri}/{controllerRoute.TrimStart('/')}";
            
            if (simple.HasValue)
            {
                url += $"?simple={simple.Value}";
            }

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Res<SystemInfoResponse>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return result?.Data;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }


} 