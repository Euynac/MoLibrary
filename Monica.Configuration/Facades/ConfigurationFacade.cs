using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Core.Results;

namespace Monica.Configuration.Facades;

/// <summary>
/// Host-facing entry point for configuration management APIs and UI consumers.
/// </summary>
public class ConfigurationFacade(
    IConfigurationManagementApi api,
    ILogger<ConfigurationFacade> logger)
{
    /// <summary>
    /// Gets the available configuration snapshots.
    /// </summary>
    public async Task<Res<List<ConfigurationDomainGroup>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false)
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
    /// Gets a single configuration option snapshot by key.
    /// </summary>
    public async Task<Res<ConfigurationOptionSnapshot>> GetOptionItemAsync(string? appid, string key)
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
    /// Gets a configuration class snapshot by key.
    /// </summary>
    public async Task<Res<ConfigurationSnapshot>> GetConfigAsync(string? appid, string key)
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
    /// Gets configuration change history.
    /// </summary>
    public async Task<Res<List<ConfigurationHistoryEntry>>> GetConfigHistoryAsync(
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
    /// Updates a configuration class or option.
    /// </summary>
    public async Task<Res> UpdateConfigAsync(ConfigurationUpdateRequest request)
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
    /// Rolls a configuration back to the specified version.
    /// </summary>
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
    /// Gets the current configuration debug view.
    /// </summary>
    public Task<Res<string[]>> GetDebugViewAsync()
    {
        try
        {
            var debugView = ConfigurationRuntime.GetDebugView().Split(Environment.NewLine);
            return Task.FromResult(Res.Ok(debugView));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置调试视图失败");
            return Task.FromResult<Res<string[]>>(Res.Fail($"获取配置调试视图失败: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets grouped configuration provider diagnostics.
    /// </summary>
    public Task<Res<List<ConfigurationProviderGroup>>> GetProvidersAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok(ConfigurationRuntime.GetProvidersGrouped()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置提供者失败");
            return Task.FromResult<Res<List<ConfigurationProviderGroup>>>(Res.Fail($"获取配置提供者失败: {ex.Message}"));
        }
    }
}
