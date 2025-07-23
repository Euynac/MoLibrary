using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Model;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.UIConfiguration.Services;

/// <summary>
/// 配置仪表板服务，提供配置中心管理功能
/// </summary>
public class ConfigurationDashboardService(
    IMoConfigurationCentre configCentre,
    IMoConfigurationDashboard dashboard,
    IMoConfigurationStores stores,
    IMoUnitOfWorkManager uowManager,
    ILogger<ConfigurationDashboardService> logger)
{
    /// <summary>
    /// 获取所有微服务配置状态
    /// </summary>
    /// <param name="mode">显示模式</param>
    /// <returns>配置状态列表</returns>
    public async Task<Res<List<DtoDomainConfigs>>> GetAllConfigStatusAsync(string? mode = null)
    {
        try
        {
            if ((await configCentre.GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
                return Res.Fail(error);

            if ((await dashboard.DashboardDisplayMode(data, mode)).IsFailed(out error, out var arranged))
                return Res.Fail(error);

            return Res.Ok(arranged);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有配置状态失败");
            return Res.Fail($"获取所有配置状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取指定配置状态
    /// </summary>
    /// <param name="appid">应用ID</param>
    /// <param name="key">配置键</param>
    /// <returns>配置状态</returns>
    public async Task<Res<DtoOptionItem>> GetOptionItemStatusAsync(string? appid, string key)
    {
        try
        {
            if ((await configCentre.GetSpecificOptionItemAsync(key, appid)).IsFailed(out var error, out var data))
                return Res.Fail(error);

            return Res.Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取指定配置状态失败");
            return Res.Fail($"获取指定配置状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置类历史
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appid">应用ID</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>配置历史</returns>
    public async Task<Res<List<DtoOptionHistory>>> GetConfigHistoryAsync(
        string? key, 
        string? appid,
        DateTime? start,
        DateTime? end)
    {
        try
        {
            using var uow = uowManager.Begin();

            if (appid != null && key != null)
            {
                var result = await stores.GetHistory(key, appid);
                return result;
            }

            if (start != null && end != null)
            {
                var result = await stores.GetHistory(start.Value, end.Value);
                return result;
            }

            var defaultResult = await stores.GetHistory(DateTime.Now.Subtract(TimeSpan.FromDays(180)), DateTime.Now);
            return defaultResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置历史失败");
            return Res.Fail($"获取配置历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新指定配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    public async Task<Res> UpdateConfigAsync(DtoUpdateConfig request)
    {
        try
        {
            using var uow = uowManager.Begin();
            var result = await configCentre.UpdateConfig(request);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置失败");
            return Res.Fail($"更新配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 回滚配置类
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appId">应用ID</param>
    /// <param name="version">版本</param>
    /// <returns>回滚结果</returns>
    public async Task<Res> RollbackConfigAsync(string key, string appId, string version)
    {
        try
        {
            using var uow = uowManager.Begin();
            var result = await configCentre.RollbackConfig(key, appId, version);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "回滚配置失败");
            return Res.Fail($"回滚配置失败: {ex.Message}");
        }
    }
}