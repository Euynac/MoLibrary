using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Dashboard.UIConfiguration.Models;
using MoLibrary.Repository.Transaction;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.UIConfiguration.Services;

/// <summary>
/// 配置服务，实现配置管理核心业务逻辑
/// </summary>
public class ConfigurationService
{
    private readonly IMoConfigurationCentre _configCentre;
    private readonly IMoConfigurationDashboard _dashboard;
    private readonly IMoConfigurationStores _stores;
    private readonly IMoConfigurationModifier _modifier;
    private readonly IMoUnitOfWorkManager _uowManager;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        IMoConfigurationCentre configCentre,
        IMoConfigurationDashboard dashboard,
        IMoConfigurationStores stores,
        IMoConfigurationModifier modifier,
        IMoUnitOfWorkManager uowManager,
        ILogger<ConfigurationService> logger)
    {
        _configCentre = configCentre;
        _dashboard = dashboard;
        _stores = stores;
        _modifier = modifier;
        _uowManager = uowManager;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有配置状态
    /// </summary>
    /// <param name="mode">显示模式</param>
    /// <returns>配置状态列表</returns>
    public async Task<Res<List<ConfigurationItemViewModel>>> GetAllConfigsAsync(string? mode = null)
    {
        try
        {
            if ((await _configCentre.GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
            {
                _logger.LogError("获取注册服务配置失败: {Error}", error);
                return Res.Fail($"获取注册服务配置失败: {error}");
            }

            if ((await _dashboard.DashboardDisplayMode(data, mode)).IsFailed(out error, out var arranged))
            {
                _logger.LogError("配置显示模式处理失败: {Error}", error);
                return Res.Fail($"配置显示模式处理失败: {error}");
            }

            var viewModels = arranged.Select(config => new ConfigurationItemViewModel
            {
                Key = config.Key ?? "Unknown",
                AppId = config.AppId ?? "Unknown",
                Value = config.Value?.ToString() ?? "",
                Type = config.Type ?? "Unknown",
                IsActive = config.IsActive,
                LastModified = config.LastModified ?? DateTime.Now,
                Description = config.Description ?? ""
            }).ToList();

            _logger.LogDebug("成功获取 {Count} 个配置项", viewModels.Count);
            return Res.Ok(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有配置失败");
            return Res.Fail($"获取所有配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取特定配置项详情
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appId">应用ID</param>
    /// <returns>配置项详情</returns>
    public async Task<Res<ConfigurationDetailViewModel>> GetConfigDetailAsync(string key, string? appId = null)
    {
        try
        {
            if ((await _configCentre.GetSpecificOptionItemAsync(key, appId)).IsFailed(out var error, out var data))
            {
                _logger.LogError("获取指定配置项失败: {Key}, {AppId}, {Error}", key, appId, error);
                return Res.Fail($"获取配置项失败: {error}");
            }

            var detail = new ConfigurationDetailViewModel
            {
                Key = data.Key ?? key,
                AppId = data.AppId ?? appId ?? "Unknown",
                Value = data.Value?.ToString() ?? "",
                Type = data.Type ?? "Unknown",
                IsActive = data.IsActive,
                LastModified = data.LastModified ?? DateTime.Now,
                Description = data.Description ?? "",
                ValidationRules = data.ValidationRules ?? new List<string>()
            };

            _logger.LogDebug("成功获取配置项详情: {Key}", key);
            return Res.Ok(detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置详情失败: {Key}", key);
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
    public async Task<Res<List<ConfigurationHistoryViewModel>>> GetConfigHistoryAsync(
        string? key = null, 
        string? appId = null, 
        DateTime? start = null, 
        DateTime? end = null)
    {
        try
        {
            using var uow = _uowManager.Begin();

            var historyResult = (key != null && appId != null)
                ? await _stores.GetHistory(key, appId)
                : (start != null && end != null)
                    ? await _stores.GetHistory(start.Value, end.Value)
                    : await _stores.GetHistory(DateTime.Now.Subtract(TimeSpan.FromDays(180)), DateTime.Now);

            if (historyResult.IsFailed(out var error, out var historyData))
            {
                _logger.LogError("获取配置历史失败: {Error}", error);
                return Res.Fail($"获取配置历史失败: {error}");
            }

            var historyViewModels = historyData.Select(h => new ConfigurationHistoryViewModel
            {
                Id = h.Id,
                Key = h.Key ?? "Unknown",
                AppId = h.AppId ?? "Unknown",
                OldValue = h.OldValue?.ToString() ?? "",
                NewValue = h.NewValue?.ToString() ?? "",
                ModifiedBy = h.ModifiedBy ?? "System",
                ModifiedTime = h.ModifiedTime,
                Operation = h.Operation ?? "Update",
                Version = h.Version ?? "1.0"
            }).ToList();

            _logger.LogDebug("成功获取 {Count} 条历史记录", historyViewModels.Count);
            return Res.Ok(historyViewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取配置历史失败");
            return Res.Fail($"获取配置历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新配置项
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    public async Task<Res<bool>> UpdateConfigAsync(ConfigurationUpdateRequest request)
    {
        try
        {
            using var uow = _uowManager.Begin();

            var updateDto = new DtoUpdateConfig
            {
                Key = request.Key,
                Value = request.Value,
                AppId = request.AppId
            };

            var result = await _configCentre.UpdateConfig(updateDto);
            
            if (result.IsFailed(out var error))
            {
                _logger.LogError("更新配置失败: {Key}, {Error}", request.Key, error);
                return Res.Fail($"更新配置失败: {error}");
            }

            _logger.LogInformation("成功更新配置: {Key}", request.Key);
            return Res.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新配置失败: {Key}", request.Key);
            return Res.Fail($"更新配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 回滚配置到指定版本
    /// </summary>
    /// <param name="request">回滚请求</param>
    /// <returns>回滚结果</returns>
    public async Task<Res<bool>> RollbackConfigAsync(ConfigurationRollbackRequest request)
    {
        try
        {
            using var uow = _uowManager.Begin();

            var result = await _configCentre.RollbackConfig(request.Key, request.AppId, request.Version);
            
            if (result.IsFailed(out var error))
            {
                _logger.LogError("回滚配置失败: {Key}, {Version}, {Error}", request.Key, request.Version, error);
                return Res.Fail($"回滚配置失败: {error}");
            }

            _logger.LogInformation("成功回滚配置: {Key} to version {Version}", request.Key, request.Version);
            return Res.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "回滚配置失败: {Key}", request.Key);
            return Res.Fail($"回滚配置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 热更新客户端配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    public async Task<Res<bool>> UpdateClientConfigAsync(ConfigurationUpdateRequest request)
    {
        try
        {
            var value = request.Value;

            if ((await _modifier.IsOptionExist(request.Key)).IsOk(out var option))
            {
                var result = await _modifier.UpdateOption(option, value);
                if (result.IsSuccess)
                {
                    _logger.LogInformation("成功更新选项配置: {Key}", request.Key);
                    return Res.Ok(true);
                }
                return Res.Fail($"更新选项配置失败: {result.Message}");
            }

            if ((await _modifier.IsConfigExist(request.Key)).IsOk(out var config))
            {
                var result = await _modifier.UpdateConfig(config, value);
                if (result.IsSuccess)
                {
                    _logger.LogInformation("成功更新配置: {Key}", request.Key);
                    return Res.Ok(true);
                }
                return Res.Fail($"更新配置失败: {result.Message}");
            }

            _logger.LogWarning("找不到配置项: {Key}", request.Key);
            return Res.Fail($"更新失败，找不到Key为{request.Key}的配置");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新客户端配置失败: {Key}", request.Key);
            return Res.Fail($"更新客户端配置失败: {ex.Message}");
        }
    }
}