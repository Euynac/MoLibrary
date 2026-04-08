using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Core.Results;

namespace Monica.Configuration.Services;

/// <summary>
/// Client-side configuration API provider.
/// Exposes configuration-management operations when running in client mode.
/// </summary>
public class LocalConfigurationManagementApi(
    IConfigurationValueWriter modifier,
    IConfigurationCatalog cardManager,
    IConfigurationHistoryStore stores,
    ILogger<LocalConfigurationManagementApi> logger) : IConfigurationManagementApi
{
    public virtual Task<Res<List<ConfigurationDomainGroup>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false)
    {
        try
        {
            var configs = cardManager.GetConfigs(onlyCurDomain);
            return Task.FromResult<Res<List<ConfigurationDomainGroup>>>(configs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有配置状态失败");
            return Task.FromResult<Res<List<ConfigurationDomainGroup>>>(Res.Fail($"获取所有配置状态失败: {ex.Message}"));
        }
    }

    public virtual Task<Res<ConfigurationOptionSnapshot>> GetOptionItemAsync(string key, string? appid = null)
    {
        try
        {
            var configs = cardManager.GetConfigs();
            var optionItem = configs
                .SelectMany(p => p.Children)
                .Where(p => appid == null || appid == p.AppId)
                .SelectMany(p => p.Children)
                .SelectMany(p => p.Items)
                .FirstOrDefault(p => p.Key == key);

            if (optionItem != null)
                return Task.FromResult<Res<ConfigurationOptionSnapshot>>(optionItem);

            return Task.FromResult<Res<ConfigurationOptionSnapshot>>("找不到相应的配置项");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置项状态失败");
            return Task.FromResult<Res<ConfigurationOptionSnapshot>>(Res.Fail($"获取配置项状态失败: {ex.Message}"));
        }
    }

    public virtual Task<Res<ConfigurationSnapshot>> GetConfigAsync(string key, string? appid = null)
    {
        try
        {
            var configs = cardManager.GetConfigs();
            var config = configs
                .SelectMany(p => p.Children)
                .Where(p => appid == null || appid == p.AppId)
                .SelectMany(p => p.Children)
                .FirstOrDefault(p => p.Name == key);

            if (config != null)
                return Task.FromResult<Res<ConfigurationSnapshot>>(config);

            return Task.FromResult<Res<ConfigurationSnapshot>>("找不到相应的配置类");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置类状态失败");
            return Task.FromResult<Res<ConfigurationSnapshot>>(Res.Fail($"获取配置类状态失败: {ex.Message}"));
        }
    }

    public Task<Res<List<ConfigurationHistoryEntry>>> GetConfigHistoryAsync(
        string? key = null,
        string? appid = null,
        DateTime? start = null,
        DateTime? end = null)
    {
        if (appid != null && key != null)
        {
            return stores.GetHistory(key, appid);
        }

        if (start != null && end != null)
        {
            return stores.GetHistory(start.Value, end.Value);
        }

        return stores.GetHistory(DateTime.Now.Subtract(TimeSpan.FromDays(180)), DateTime.Now);
    }

    public virtual async Task<Res<ConfigurationUpdateResult>> UpdateConfigAsync(ConfigurationUpdateRequest request)
    {
        try
        {
            var value = request.Value;

            if ((await modifier.IsOptionExist(request.Key)).IsOk(out var option))
            {
                if((await modifier.UpdateOption(option, value)).IsFailed(out var error, out var data))
                    return error;
                if((await SaveHistory(data, request.AppId)).IsFailed(out error, out var result))
                    return error;
                return result;
            }

            if ((await modifier.IsConfigExist(request.Key)).IsOk(out var config))
            {
                if((await modifier.UpdateConfig(config, value)).IsFailed(out var error, out var data))
                    return error;
                if((await SaveHistory(data, request.AppId)).IsFailed(out error, out var result))
                    return error;
                return result;
            }

            return Res.Fail($"更新失败,找不到Key为{request.Key}的配置");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置失败: {Key}", request.Key);
            return Res.Fail($"更新配置失败: {ex.Message}");
        }
    }

    public async Task<Res<ConfigurationUpdateResult>> RollbackConfigAsync(string key, string appid, string version)
    {
        if ((await stores.GetHistory(key, appid, version)).IsFailed(out var error, out var data)) return error;

        var req = new ConfigurationUpdateRequest()
        {
            AppId = data.AppId,
            Key = data.Key,
            Value = data.OldValue
        };
        return await UpdateConfigAsync(req);
    }

    protected async Task<Res<ConfigurationUpdateResult>> SaveHistory(ConfigurationUpdateResult res, string appid)
    {
        res.AppId = appid;
        if((await stores.SaveUpdate(res)).IsFailed(out var err)) return err;
        return res;
    }
}
