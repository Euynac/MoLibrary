using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UIRegisterCentre.Services;

public class RegisterCentreService(
    ILogger<RegisterCentreService> logger,
    IServiceProvider serviceProvider)
{
    public async Task<Res<List<RegisteredServiceStatus>>> GetServicesStatusAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return "当前服务未配置为注册中心服务端";
            }

            return await registerCentreServer.GetServicesStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取服务状态失败");
            return $"获取服务状态失败: {ex.Message}";
        }
    }

    public async Task<Res> UnregisterAllAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return "当前服务未配置为注册中心服务端";
            }

            return await registerCentreServer.UnregisterAll();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清空所有注册失败");
            return $"清空所有注册失败: {ex.Message}";
        }
    }

    public async Task<Res> RegisterServiceAsync(ServiceRegisterInfo info)
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return "当前服务未配置为注册中心服务端";
            }

            return await registerCentreServer.Register(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "注册服务失败");
            return $"注册服务失败: {ex.Message}";
        }
    }

    public async Task<Res> TestReconnectAsync()
    {
        try
        {
            var connector = serviceProvider.GetService<IRegisterCentreServerConnector>();
            var client = serviceProvider.GetService<IRegisterCentreClient>();
            
            if (connector == null || client == null)
            {
                return "当前服务未配置为注册中心客户端";
            }

            return await connector.Register(client.GetServiceStatus());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试重连失败");
            return $"测试重连失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取合并的服务状态列表（包含预定义服务和已注册服务）
    /// </summary>
    /// <returns>合并后的服务状态列表</returns>
    public async Task<Res<List<RegisteredServiceStatus>>> GetMergedServicesStatusAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            var infoProvider = serviceProvider.GetService<IRegisterCentreInfoProvider>();

            // 获取已注册的服务状态
            List<RegisteredServiceStatus> registeredServices = [];
            if (registerCentreServer != null)
            {
                if (!(await registerCentreServer.GetServicesStatus()).IsFailed(out _, out var data))
                {
                    registeredServices = data ?? [];
                }
            }

            // 获取预定义的服务信息
            List<ServiceInfo> preloadedServices = [];
            if (infoProvider != null)
            {
                preloadedServices = await infoProvider.GetPreloadedServicesAsync();
            }

            // 合并服务列表
            var mergedServices = new List<RegisteredServiceStatus>(registeredServices);

            // 检查预定义服务是否已在注册服务中存在，如果不存在则添加
            foreach (var preloadedService in preloadedServices)
            {
                var existingService = registeredServices.FirstOrDefault(r => r.AppId == preloadedService.AppId);
                if (existingService == null)
                {
                    // 创建一个离线状态的服务条目
                    mergedServices.Add(new RegisteredServiceStatus
                    {
                        AppId = preloadedService.AppId,
                        AppName = preloadedService.AppName ?? preloadedService.AppId,
                        // 其他属性保持默认值，Instances为空字典表示离线状态
                    });
                }
            }

            return mergedServices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取合并服务状态失败");
            return $"获取合并服务状态失败: {ex.Message}";
        }
    }
}