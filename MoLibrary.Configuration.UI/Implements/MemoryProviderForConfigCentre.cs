using MoLibrary.Configuration.Interfaces;
using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.UI.Interfaces;
using MoLibrary.Configuration.UI.Model;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.UI.Implements;

/// <summary>
/// Configuration center provider that uses RegisterCentre's registration state manager
/// to get the list of registered services and invoke their configuration endpoints.
/// </summary>
/// <remarks>
/// This class implements <see cref="IMoConfigurationCentre"/>.
/// It provides functionality for retrieving registered service configurations,
/// updating configurations, and rolling back configurations.
/// </remarks>
public class MemoryProviderForConfigCentre(
    IMoConfigurationModifier modifier,
    IMoConfigurationStores stores,
    IMoConfigurationCardManager manager,
    IConfigurationCentreServiceInvoker invoker,
    IRegistrationStateManager stateManager) : IMoConfigurationCentre
{
    private static (List<DtoDomainConfigs>? Data, DateTime CachedAt) _cache;
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    public async Task<Res<List<DtoDomainConfigs>>> GetRegisteredServicesConfigsAsync()
    {
        if (_cache.Data != null && DateTime.UtcNow - _cache.CachedAt < _cacheTtl)
            return _cache.Data;

        // Get all instances from the state manager
        var instances = await stateManager.GetAllLeaderInstancesAsync();

        // Extract AppIds, ordered by build time (newest first) so newest version is selected in Distinct
        var list = instances
            .OrderByDescending(i => i.BuildTime)
            .Select(i => i.ServiceName)
            .ToList();

        var res = await invoker.GetRegisteredServicesConfigsAsync(list);
        if (res.IsFailed(out var error, out var statusList)) return error;

        statusList.AddRange(manager.GetDomainConfigs());
        if ((await WashDomainConfigs(statusList)).IsFailed(out error, out var configs)) return error;
        _cache = (configs, DateTime.UtcNow);
        return configs;
    }

    public async Task<Res<DtoOptionItem>> GetSpecificOptionItemAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).SelectMany(p => p.Children).Where(p => appid == null || appid == p.AppId).SelectMany(p => p.Items)
            .FirstOrDefault(p => p.Key == key);
        if (dtoConfig != null) return dtoConfig;

        return "找不到相应的配置项";
    }

    public async Task<Res<DtoConfig>> GetSpecificConfigStatusAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).SelectMany(p => p.Children)
            .FirstOrDefault(p => (appid == null || p.AppId == appid) && p.Name == key);
        if (dtoConfig != null) return dtoConfig;

        return "找不到相应的配置类";
    }

    public async Task<Res> RollbackConfig(string key, string appid, string version)
    {
        if ((await stores.GetHistory(key, appid, version)).IsFailed(out var error, out var data)) return error;

        var req = new DtoUpdateConfig()
        {
            AppId = data.AppId,
            Key = data.Key,
            Value = data.OldValue
        };
        return await UpdateConfig(req);
    }

    private async Task<Res> SaveHistory(Res<DtoUpdateConfigRes> res, string projectName)
    {
        if (res.IsFailed(out var err, out var data)) return err;
        data.AppId = projectName;
        return await stores.SaveUpdate(data);
    }

    public async Task<Res> UpdateConfig(DtoUpdateConfig req)
    {
        _cache = default;
        // If configuration center node has the config item, modify it locally
        if ((await modifier.IsOptionExist(req.Key)).IsOk(out var option))
        {
            return (await SaveHistory(await modifier.UpdateOption(option, req.Value), req.AppId)).IsFailed(out var error) ? error : Res.Ok("路由到中心节点保存成功");
        }

        if ((await modifier.IsConfigExist(req.Key)).IsOk(out var config))
        {
            return (await SaveHistory(await modifier.UpdateConfig(config, req.Value), req.AppId)).IsFailed(out var error) ? error : Res.Ok("路由到中心节点保存成功");
        }
        if ((await invoker.UpdateRemoteConfigAsync(req.AppId, req))
            .IsFailed(out var remoteError, out var data)) return remoteError;

        return (await SaveHistory(Res.Ok(data), req.AppId)).IsFailed(out var historyErr) ? historyErr : Res.Ok($"路由到{req.AppId}节点保存成功");
    }

    public Task<Res<List<DtoDomainConfigs>>> WashDomainConfigs(List<DtoDomainConfigs> configs)
    {
        var group = configs.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.ToList());
        var finalDomainConfigs = new List<DtoDomainConfigs>();
        foreach (var item in group)
        {
            var tmp = item.Value.First();
            tmp.Children = item.Value.SelectMany(p => p.Children).DistinctBy(p => p.Name).ToList();
            finalDomainConfigs.Add(tmp);
        }

        return Task.FromResult<Res<List<DtoDomainConfigs>>>(finalDomainConfigs);
    }
}
