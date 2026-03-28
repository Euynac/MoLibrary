using Microsoft.Extensions.Logging;
using Monica.Configuration.Interfaces;
using Monica.Configuration.Model;
using Monica.Configuration.UI.Interfaces;
using Monica.Configuration.UI.Model;
using Monica.Core.Results;

namespace Monica.Configuration.UI.Implements;

/// <summary>
/// Client-side configuration API provider.
/// Exposes configuration-management operations when running in client mode.
/// </summary>
public class ConfigurationClientApiProvider(
    IMoConfigurationModifier modifier,
    IMoConfigurationCardManager cardManager,
    IMoConfigurationStores stores,
    ILogger<ConfigurationClientApiProvider> logger) : IMoConfigurationApi
{
    public virtual Task<Res<List<DtoDomainGroup>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false)
    {
        try
        {
            var configs = cardManager.GetConfigs(onlyCurDomain);
            return Task.FromResult<Res<List<DtoDomainGroup>>>(configs);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取所有配置状态失败");
            return Task.FromResult<Res<List<DtoDomainGroup>>>(Res.Fail($"获取所有配置状态失败: {ex.Message}"));
        }
    }

    public virtual async Task<Res<DtoOptionItem>> GetOptionItemAsync(string key, string? appid = null)
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
                return optionItem;

            return "找不到相应的配置项";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置项状态失败");
            return Res.Fail($"获取配置项状态失败: {ex.Message}");
        }
    }

    public virtual async Task<Res<DtoConfig>> GetConfigAsync(string key, string? appid = null)
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
                return config;

            return "找不到相应的配置类";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取配置类状态失败");
            return Res.Fail($"获取配置类状态失败: {ex.Message}");
        }
    }

    public Task<Res<List<DtoOptionHistory>>> GetConfigHistoryAsync(
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

    public virtual async Task<Res<DtoUpdateConfigRes>> UpdateConfigAsync(DtoUpdateConfig request)
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

    public async Task<Res<DtoUpdateConfigRes>> RollbackConfigAsync(string key, string appid, string version)
    {
        if ((await stores.GetHistory(key, appid, version)).IsFailed(out var error, out var data)) return error;

        var req = new DtoUpdateConfig()
        {
            AppId = data.AppId,
            Key = data.Key,
            Value = data.OldValue
        };
        return await UpdateConfigAsync(req);
    }

    protected async Task<Res<DtoUpdateConfigRes>> SaveHistory(DtoUpdateConfigRes res, string appid)
    {
        res.AppId = appid;
        if((await stores.SaveUpdate(res)).IsFailed(out var err)) return err;
        return res;
    }
}
