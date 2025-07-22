using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Model;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.UIConfiguration.Services;

/// <summary>
/// 配置服务，实现配置管理核心业务逻辑
/// </summary>
/// <param name="configCentre">配置中心接口</param>
/// <param name="dashboard">配置仪表板接口</param>
/// <param name="stores">配置存储接口</param>
/// <param name="modifier">配置修改器接口</param>
/// <param name="uowManager">工作单元管理器</param>
/// <param name="logger">日志记录器</param>
public class ConfigurationService(
    IMoConfigurationCentre configCentre,
    IMoConfigurationDashboard dashboard,
    IMoConfigurationStores stores,
    IMoConfigurationModifier modifier,
    IMoUnitOfWorkManager uowManager,
    ILogger<ConfigurationService> logger)
{
    /// <summary>
    /// 获取所有配置状态
    /// </summary>
    /// <param name="mode">显示模式</param>
    /// <returns>配置状态列表</returns>
    public async Task<Res<List<DtoOptionItem>>> GetAllConfigsAsync(string? mode = null)
    {
        try
        {
            if ((await configCentre.GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
            {
                logger.LogError("获取注册服务配置失败: {Error}", error);
                return Res.Fail($"获取注册服务配置失败: {error}");
            }

            if ((await dashboard.DashboardDisplayMode(data, mode)).IsFailed(out error, out var arranged))
            {
                logger.LogError("配置显示模式处理失败: {Error}", error);
                return Res.Fail($"配置显示模式处理失败: {error}");
            }

            var allItems = new List<DtoOptionItem>();
            foreach (var domainConfig in arranged)
            {
                foreach (var serviceConfig in domainConfig.Children)
                {
                    foreach (var config in serviceConfig.Children)
                    {
                        allItems.AddRange(config.Items);
                    }
                }
            }

            logger.LogDebug("成功获取 {Count} 个配置项", allItems.Count);
            return Res.Ok(allItems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有配置失败");
            return Res.Fail($"获取所有配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取特定配置项详情
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appId">应用ID</param>
    /// <returns>配置项详情</returns>
    public async Task<Res<DtoOptionItem>> GetConfigDetailAsync(string key, string? appId = null)
    {
        try
        {
            if ((await configCentre.GetSpecificOptionItemAsync(key, appId)).IsFailed(out var error, out var data))
            {
                logger.LogError("获取指定配置项失败: {Key}, {AppId}, {Error}", key, appId, error);
                return Res.Fail($"获取配置项失败: {error}");
            }

            logger.LogDebug("成功获取配置项详情: {Key}", key);
            return Res.Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置详情失败: {Key}", key);
            return Res.Fail($"获取配置详情失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置历史记录
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appId">应用ID</param>
    /// <param name="start">开始时间</param>
    /// <param name="end">结束时间</param>
    /// <returns>历史记录列表</returns>
    public async Task<Res<List<DtoOptionHistory>>> GetConfigHistoryAsync(
        string? key = null, 
        string? appId = null, 
        DateTime? start = null, 
        DateTime? end = null)
    {
        try
        {
            using var uow = uowManager.Begin();

            var historyResult = (key != null && appId != null)
                ? await stores.GetHistory(key, appId)
                : (start != null && end != null)
                    ? await stores.GetHistory(start.Value, end.Value)
                    : await stores.GetHistory(DateTime.Now.Subtract(TimeSpan.FromDays(180)), DateTime.Now);

            if (historyResult.IsFailed(out var error, out var historyData))
            {
                logger.LogError("获取配置历史失败: {Error}", error);
                return Res.Fail($"获取配置历史失败: {error}");
            }

            logger.LogDebug("成功获取 {Count} 条历史记录", historyData.Count);
            return Res.Ok(historyData);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置历史失败");
            return Res.Fail($"获取配置历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新配置项
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    /// <param name="appId">应用ID</param>
    /// <returns>更新结果</returns>
    public async Task<Res<bool>> UpdateConfigAsync(string key, object value, string? appId = null)
    {
        try
        {
            using var uow = uowManager.Begin();

            var updateDto = new DtoUpdateConfig
            {
                Key = key,
                Value = JsonNode.Parse(JsonSerializer.Serialize(value)),
                AppId = appId
            };

            var result = await configCentre.UpdateConfig(updateDto);
            
            if (result.IsFailed(out var error))
            {
                logger.LogError("更新配置失败: {Key}, {Error}", key, error);
                return Res.Fail($"更新配置失败: {error}");
            }

            logger.LogInformation("成功更新配置: {Key}", key);
            return Res.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置失败: {Key}", key);
            return Res.Fail($"更新配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 回滚配置到指定版本
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appId">应用ID</param>
    /// <param name="version">目标版本</param>
    /// <returns>回滚结果</returns>
    public async Task<Res<bool>> RollbackConfigAsync(string key, string appId, string version)
    {
        try
        {
            using var uow = uowManager.Begin();

            var result = await configCentre.RollbackConfig(key, appId, version);
            
            if (result.IsFailed(out var error))
            {
                logger.LogError("回滚配置失败: {Key}, {Version}, {Error}", key, version, error);
                return Res.Fail($"回滚配置失败: {error}");
            }

            logger.LogInformation("成功回滚配置: {Key} to version {Version}", key, version);
            return Res.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "回滚配置失败: {Key}", key);
            return Res.Fail($"回滚配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 热更新客户端配置
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="value">配置值</param>
    /// <returns>更新结果</returns>
    public async Task<Res<bool>> UpdateClientConfigAsync(string key, object value)
    {
        try
        {
            var node = JsonNode.Parse(JsonSerializer.Serialize(value));
            if ((await modifier.IsOptionExist(key)).IsOk(out var option))
            {
                if ((await modifier.UpdateOption(option, node)).IsFailed(out var error))
                {
                    logger.LogError("更新选项配置失败: {Key}, {Error}", key, error);
                    return Res.Fail($"更新选项配置失败: {error}");
                }
                logger.LogInformation("成功更新选项配置: {Key}", key);
                return Res.Ok(true);
            }

            if ((await modifier.IsConfigExist(key)).IsOk(out var config))
            {
                if ((await modifier.UpdateConfig(config, node)).IsFailed(out var error))
                {
                    logger.LogError("更新配置失败: {Key}, {Error}", key, error);
                    return Res.Fail($"更新配置失败: {error}");
                }
                logger.LogInformation("成功更新配置: {Key}", key);
                return Res.Ok(true);
            }

            logger.LogWarning("找不到配置项: {Key}", key);
            return Res.Fail($"更新失败，找不到Key为{key}的配置");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新客户端配置失败: {Key}", key);
            return Res.Fail($"更新客户端配置失败: {ex.Message}");
        }
    }
}