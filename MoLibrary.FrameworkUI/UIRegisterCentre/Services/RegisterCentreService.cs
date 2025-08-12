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
                return Res.Fail("当前服务未配置为注册中心服务端");
            }

            return await registerCentreServer.GetServicesStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取服务状态失败");
            return Res.Fail($"获取服务状态失败: {ex.Message}");
        }
    }

    public async Task<Res> UnregisterAllAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return Res.Fail("当前服务未配置为注册中心服务端");
            }

            return await registerCentreServer.UnregisterAll();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清空所有注册失败");
            return Res.Fail($"清空所有注册失败: {ex.Message}");
        }
    }

    public async Task<Res> RegisterServiceAsync(ServiceRegisterInfo info)
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return Res.Fail("当前服务未配置为注册中心服务端");
            }

            return await registerCentreServer.Register(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "注册服务失败");
            return Res.Fail($"注册服务失败: {ex.Message}");
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
                return Res.Fail("当前服务未配置为注册中心客户端");
            }

            return await connector.Register(client.GetServiceStatus());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试重连失败");
            return Res.Fail($"测试重连失败: {ex.Message}");
        }
    }
}