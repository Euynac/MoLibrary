using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.UI.Interfaces;
using MoLibrary.Configuration.UI.Model;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.UI.Implements;

/// <summary>
/// 配置中心API提供者，使用注册中心的状态管理器获取已注册服务列表并调用其配置端点
/// </summary>
public class ConfigurationCentreApiProvider(
    IMoConfigurationModifier modifier,
    IMoConfigurationCardManager manager,
    IMoConfigurationStores stores,
    IMoConfigurationDashboard dashboard,
    IConfigurationCentreServiceInvoker invoker,
    IRegistrationStateManager stateManager,
    ILogger<ConfigurationClientApiProvider> logger)
    : ConfigurationClientApiProvider(modifier, manager, stores, logger)
{
    private static (List<DtoDomainConfigs>? Data, DateTime CachedAt) _cache;
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    public override async Task<Res<List<DtoDomainConfigs>>> GetAllConfigStatusAsync(string? mode = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
            return Res.Fail(error);

        if ((await dashboard.DashboardDisplayMode(data, mode)).IsFailed(out error, out var arranged))
            return Res.Fail(error);

        return Res.Ok(arranged);
    }

    public override async Task<Res<DtoOptionItem>> GetOptionItemStatusAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).SelectMany(p => p.Children).Where(p => appid == null || appid == p.AppId).SelectMany(p => p.Items)
            .FirstOrDefault(p => p.Key == key);
        if (dtoConfig != null) return dtoConfig;

        return "找不到相应的配置项";
    }

    public override async Task<Res<DtoConfig>> GetConfigStatusAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).SelectMany(p => p.Children)
            .FirstOrDefault(p => (appid == null || p.AppId == appid) && p.Name == key);
        if (dtoConfig != null) return dtoConfig;

        return "找不到相应的配置类";
    }

    public override async Task<Res<DtoUpdateConfigRes>> UpdateConfigAsync(DtoUpdateConfig req)
    {
        _cache = default;
        if((await invoker.UpdateRemoteConfigAsync(req.AppId, req)).IsFailed(out var remoteError, out var remoteData))
            return remoteError;
        return remoteData;
    }

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
