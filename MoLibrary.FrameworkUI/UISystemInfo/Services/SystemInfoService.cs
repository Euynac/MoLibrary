using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MoLibrary.FrameworkUI.UISystemInfo.Controllers;
using MoLibrary.FrameworkUI.UISystemInfo.Models;
using MoLibrary.Tool.MoResponse;
using System.Text.Json;

namespace MoLibrary.FrameworkUI.UISystemInfo.Services;

/// <summary>
/// 系统信息服务，用于调用自身的Controller
/// </summary>
public class SystemInfoService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;

    public SystemInfoService(HttpClient httpClient, NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
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
            // 构建API URL
            var controllerRoute = GetControllerRouteTemplate(typeof(ModuleSystemInfoController));
            var baseUri = _navigationManager.BaseUri.TrimEnd('/');
            var url = $"{baseUri}/{controllerRoute.TrimStart('/')}/system/info";
            
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

    /// <summary>
    /// 获取Controller路由模板
    /// </summary>
    /// <param name="controllerType">Controller类型</param>
    /// <returns>路由模板</returns>
    private static string GetControllerRouteTemplate(Type controllerType)
    {
        // 这里简化处理，直接使用约定路由
        var controllerName = controllerType.Name.Replace("Controller", "");
        if (controllerName.StartsWith("Module"))
        {
            controllerName = controllerName.Substring(6); // 移除"Module"前缀
        }
        return $"api/v1/{controllerName}";
    }
} 