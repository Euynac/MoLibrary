using Microsoft.Extensions.Logging;
using Monica.Configuration.Model;
using Monica.Configuration.UI.Interfaces;
using Monica.Configuration.UI.Model;
using Monica.Tool.MoResponse;

namespace Monica.Configuration.UI.Services;

/// <summary>
/// 统一配置UI服务，提供配置管理功能（支持配置中心和客户端模式）
/// </summary>
public class ConfigurationUIService(
    IMoConfigurationApi api,
    ILogger<ConfigurationUIService> logger)
{
    /// <summary>
    /// 获取所有配置状态信息
    /// </summary>
    /// <param name="mode">显示模式（可选）</param>
    /// <param name="onlyCurDomain"></param>
    /// <returns>配置状态列表</returns>
    public async Task<Res<List<DtoDomainConfigs>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false)
    {
        try
        {
            return await api.GetConfigsAsync(mode, onlyCurDomain);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有配置状态失败");
            return Res.Fail($"获取所有配置状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定配置项状态信息
    /// </summary>
    /// <param name="appid">应用ID（可选）</param>
    /// <param name="key">配置键</param>
    /// <returns>配置项状态</returns>
    public async Task<Res<DtoOptionItem>> GetOptionItemAsync(string? appid, string key)
    {
        try
        {
            return await api.GetOptionItemAsync(key, appid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置项状态失败");
            return Res.Fail($"获取配置项状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定配置类状态信息
    /// </summary>
    /// <param name="appid">应用ID（可选）</param>
    /// <param name="key">配置键</param>
    /// <returns>配置类状态</returns>
    public async Task<Res<DtoConfig>> GetConfigAsync(string? appid, string key)
    {
        try
        {
            return await api.GetConfigAsync(key, appid);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置类状态失败");
            return Res.Fail($"获取配置类状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置历史记录
    /// </summary>
    /// <param name="key">配置键（可选）</param>
    /// <param name="appid">应用ID（可选）</param>
    /// <param name="start">开始时间（可选）</param>
    /// <param name="end">结束时间（可选）</param>
    /// <returns>配置历史列表</returns>
    public async Task<Res<List<DtoOptionHistory>>> GetConfigHistoryAsync(
        string? key,
        string? appid,
        DateTime? start,
        DateTime? end)
    {
        try
        {
            return await api.GetConfigHistoryAsync(key, appid, start, end);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置历史失败");
            return Res.Fail($"获取配置历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    public async Task<Res> UpdateConfigAsync(DtoUpdateConfig request)
    {
        try
        {
            return await api.UpdateConfigAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置失败");
            return Res.Fail($"更新配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 回滚配置到指定版本
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appId">应用ID</param>
    /// <param name="version">版本号</param>
    /// <returns>回滚结果</returns>
    public async Task<Res> RollbackConfigAsync(string key, string appId, string version)
    {
        try
        {
            return await api.RollbackConfigAsync(key, appId, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "回滚配置失败");
            return Res.Fail($"回滚配置失败: {ex.Message}");
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
    public async Task<Res<List<DtoConfigurationProviderGroup>>> GetProvidersAsync()
    {
        try
        {
            var providers = MoConfigurationManager.GetProvidersGrouped();
            if (providers == null || providers.Count == 0)
            {
                return Res.Ok(new List<DtoConfigurationProviderGroup>());
            }
            return Res.Ok(providers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置提供者失败");
            return Res.Fail($"获取配置提供者失败: {ex.Message}");
        }
    }
}
