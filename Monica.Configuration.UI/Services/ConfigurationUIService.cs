using Microsoft.Extensions.Logging;
using Monica.Configuration.Model;
using Monica.Configuration.UI.Interfaces;
using Monica.Configuration.UI.Model;
using Monica.Tool.Results;

namespace Monica.Configuration.UI.Services;

/// <summary>
/// Unify configuration UI services and provide configuration management functions (supports configuration center and client mode)
/// </summary>
public class ConfigurationUIService(
    IMoConfigurationApi api,
    ILogger<ConfigurationUIService> logger)
{
    /// <summary>
    /// Get all configuration status information
    /// </summary>
    /// <param name="mode">Display mode (optional)</param>
    /// <param name="onlyCurDomain"></param>
    /// <returns>Configuration status list</returns>
    public async Task<Res<List<DtoDomainGroup>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false)
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
    /// Get the status information of the specified configuration item
    /// </summary>
    /// <param name="appid">Application ID (optional)</param>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration item status</returns>
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
    /// Get status information of specified configuration class
    /// </summary>
    /// <param name="appid">Application ID (optional)</param>
    /// <param name="key">Configuration key</param>
    /// <returns>Configuration class status</returns>
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
    /// Get configuration history
    /// </summary>
    /// <param name="key">Configuration key (optional)</param>
    /// <param name="appid">Application ID (optional)</param>
    /// <param name="start">Start time (optional)</param>
    /// <param name="end">End time (optional)</param>
    /// <returns>Configuration history list</returns>
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
    /// Update configuration
    /// </summary>
    /// <param name="request">Update request</param>
    /// <returns>Update results</returns>
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
    /// Roll back configuration to specified version
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="appId">Application ID</param>
    /// <param name="version">Version number</param>
    /// <returns>Rollback results</returns>
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
    /// Get configuration debug view
    /// </summary>
    /// <returns>Configure debugging information</returns>
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
    /// Get configuration provider information
    /// </summary>
    /// <returns>Configuration provider list</returns>
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

    /// <summary>
    /// Analyze the source consistency of configuration items in configuration classes
    /// </summary>
    /// <param name="config">Configuration class</param>
    /// <returns>Source analysis results</returns>
    public ConfigSourceAnalysis AnalyzeConfigSourceConsistency(DtoConfig config)
    {
        var analysis = new ConfigSourceAnalysis
        {
            ConfigName = config.Name,
            ConfigTitle = config.Title
        };

        if (config.Items.Count == 0)
        {
            analysis.IsConsistent = true;
            return analysis;
        }

        // Group items by their final effective source
        var sourceGroups = config.Items
            .GroupBy(item => new { item.Provider, item.Source })
            .ToList();

        analysis.IsConsistent = sourceGroups.Count == 1;

        if (!analysis.IsConsistent)
        {
            // Build detailed inconsistency information
            foreach (var group in sourceGroups)
            {
                var sourceInfo = new ConfigSourceGroup
                {
                    Provider = group.Key.Provider ?? "Unknown",
                    Source = group.Key.Source ?? string.Empty,
                    Items = group.Select(item => new ConfigItemSourceInfo
                    {
                        Key = item.Key,
                        Title = item.Title,
                        Name = item.Name
                    }).ToList()
                };
                analysis.SourceGroups.Add(sourceInfo);
            }
        }

        return analysis;
    }
}
