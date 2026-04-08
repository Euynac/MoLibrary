using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Core.Results;
using Monica.ServiceDiscovery.Abstractions;

namespace Monica.Configuration.Services;

/// <summary>
/// Configuration-center API provider.
/// Uses the registration-state manager to discover registered services and invoke their configuration endpoints.
/// </summary>
public class RegistryConfigurationManagementApi(
    IConfigurationValueWriter modifier,
    IConfigurationCatalog manager,
    IConfigurationHistoryStore stores,
    IConfigurationRemoteGateway invoker,
    IRegistrationStateManager stateManager,
    IServiceDiscoveryClientInfo clientInfo,
    ILogger<LocalConfigurationManagementApi> logger)
    : LocalConfigurationManagementApi(modifier, manager, stores, logger)
{
    private readonly IConfigurationCatalog _manager = manager;
    private static (List<ConfigurationDomainGroup>? Data, DateTime CachedAt) _cache;
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    public override async Task<Res<List<ConfigurationDomainGroup>>> GetConfigsAsync(string? mode = null,
        bool onlyCurDomain = false)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data))
            return Res.Fail(error);

        return Res.Ok(data);
    }

    public override async Task<Res<ConfigurationOptionSnapshot>> GetOptionItemAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).Where(p => appid == null || appid == p.AppId).SelectMany(p => p.Children).SelectMany(p => p.Items)
            .FirstOrDefault(p => p.Key == key);
        if (dtoConfig != null) return dtoConfig;

        return "找不到相应的配置项";
    }

    public override async Task<Res<ConfigurationSnapshot>> GetConfigAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).Where(p => appid == null || appid == p.AppId).SelectMany(p => p.Children)
            .FirstOrDefault(p => p.Name == key);
        if (dtoConfig != null) return dtoConfig;

        return "找不到相应的配置类";
    }

    public override async Task<Res<ConfigurationUpdateResult>> UpdateConfigAsync(ConfigurationUpdateRequest req)
    {
        _cache = default;
        var curAppid = clientInfo.GetServiceStatus().ServiceName;
        if (curAppid == req.AppId)
            return await base.UpdateConfigAsync(req);
        
        if((await invoker.UpdateRemoteConfigAsync(req.AppId, req)).IsFailed(out var remoteError, out var remoteData))
            return remoteError;
        return remoteData;
    }

    public async Task<Res<List<ConfigurationDomainGroup>>> GetRegisteredServicesConfigsAsync()
    {
        if (_cache.Data != null && DateTime.UtcNow - _cache.CachedAt < _cacheTtl)
            return _cache.Data;

        // Get all instances from the state manager
        var instances = await stateManager.GetAllLeaderInstancesAsync();
        var curAppid = clientInfo.GetServiceStatus().ServiceName;
        // Extract AppIds, ordered by build time (newest first) so newest version is selected in Distinct
        var list = instances
            .Where(p=>p.ServiceName != curAppid)
            .OrderByDescending(i => i.BuildTime)
            .Select(i => i.ServiceName)
            .ToList();

        if ((await invoker.GetRegisteredServicesConfigsAsync(list)).IsFailed(out var error, out var statusList)) return error;

        statusList.AddRange(_manager.GetConfigs());
        if ((await WashDomainConfigs(statusList)).IsFailed(out error, out var configs)) return error;
        _cache = (configs, DateTime.UtcNow);
        return configs;
    }

    public Task<Res<List<ConfigurationDomainGroup>>> WashDomainConfigs(List<ConfigurationDomainGroup> configs)
    {
        var group = configs.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.ToList());
        var finalDomainConfigs = new List<ConfigurationDomainGroup>();
        foreach (var item in group)
        {
            var tmp = item.Value.First();
            tmp.Children = item.Value.SelectMany(p => p.Children).DistinctBy(p => p.Name).ToList();
            finalDomainConfigs.Add(tmp);
        }

        return Task.FromResult<Res<List<ConfigurationDomainGroup>>>(finalDomainConfigs);
    }
}
