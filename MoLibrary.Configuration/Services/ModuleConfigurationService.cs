using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Configuration.Model;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Services;

/// <summary>
/// 模块配置服务，提供基础配置管理功能
/// </summary>
public class ModuleConfigurationService(
    IMoConfigurationCardManager configCardManager,
    ILogger<ModuleConfigurationService> logger)
{
    /// <summary>
    /// 获取热配置状态信息
    /// </summary>
    /// <param name="onlyCurDomain">是否只获取当前域配置</param>
    /// <returns>配置状态信息</returns>
    public async Task<Res<List<DtoDomainConfigs>>> GetConfigStatusAsync(bool? onlyCurDomain = null)
    {
        try
        {
            var result = configCardManager.GetDomainConfigs(onlyCurDomain);
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置状态失败");
            return Res.Fail($"获取配置状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置调试视图
    /// </summary>
    /// <returns>配置调试信息</returns>
    public async Task<Res<string[]>> GetDebugViewAsync()
    {
        try
        {
            var debugView = MoConfigurationManager.GetDebugView().Split(Environment.NewLine);
            return Res.Ok(debugView);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置调试视图失败");
            return Res.Fail($"获取配置调试视图失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置提供者信息
    /// </summary>
    /// <returns>配置提供者列表</returns>
    public async Task<Res<object>> GetProvidersAsync()
    {
        try
        {
            var providers = MoConfigurationManager.GetProviders();
            return Res.Ok(providers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置提供者失败");
            return Res.Fail($"获取配置提供者失败: {ex.Message}");
        }
    }
}