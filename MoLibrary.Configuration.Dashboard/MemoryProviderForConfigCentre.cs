using Microsoft.AspNetCore.Http;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Interfaces;
using MoLibrary.Configuration.Model;
using MoLibrary.RegisterCentre.Implements;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard;

/// <summary>
/// Represents a memory-based provider for managing configuration in the configuration center.
/// </summary>
/// <remarks>
/// This class extends <see cref="MemoryProviderForRegisterCentre"/> 
/// and implements <see cref="IMoConfigurationCentre"/>.
/// It provides functionality for registering services, retrieving registered service configurations, 
/// updating configurations, and rolling back configurations.
/// </remarks>
/// <seealso cref="MemoryProviderForRegisterCentre"/>
/// <seealso cref="IMoConfigurationCentre"/>
public class MemoryProviderForConfigCentre(
    IHttpContextAccessor accessor, IMoConfigurationModifier modifier, 
    IMoConfigurationStores stores, IMoConfigurationCardManager manager, IRegisterCentreClientConnector connector) : MemoryProviderForRegisterCentre(accessor, connector),  IMoConfigurationCentre
{
    private static List<DtoDomainConfigs>? _cache;
    private readonly IRegisterCentreClientConnector _connector = connector;


    public override Task<Res> Register(ServiceRegisterInfo req)
    {
        _cache = null;
        return base.Register(req);
    }

    public async Task<Res<List<DtoDomainConfigs>>> GetRegisteredServicesConfigsAsync()
    {
        if (_cache != null) return _cache;
 
        //这里通过构建时间排序，来保证最新版本的微服务配置优先读取。在最后Distinct的时候优先被选择。
        var list =  Dict.Values.OrderByDescending(p => p.BuildTime).Select(p => p.AppId).ToList();

        var res = await _connector.GetAsync<Res<List<DtoDomainConfigs>>>(list,
            $"{MoConfigurationConventions.GetConfigStatus}?onlyCurDomain=true");

        var statusList = res.Values.Where(p => p.IsOk() && p.Data != null && p.Data.IsOk() && p.Data.Data != null).SelectMany(p=>p.Data!.Data!).ToList();
        statusList.AddRange(manager.GetDomainConfigs());
        if ((await WashDomainConfigs(statusList)).IsFailed(out var error, out var configs)) return error;
        _cache = configs;
        return configs;
    }

    public async Task<Res<DtoOptionItem>> GetSpecificOptionItemAsync(string key, string? appid = null)
    {
        if ((await GetRegisteredServicesConfigsAsync()).IsFailed(out var error, out var data)) return error;

        var dtoConfig = data.SelectMany(p => p.Children).SelectMany(p => p.Children).Where(p=>appid == null || appid == p.AppId).SelectMany(p=>p.Items)
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
        //如果中心节点有配置项，直接通过本地修改
        if ((await modifier.IsOptionExist(req.Key)).IsOk(out var option))
        {
             if ((await SaveHistory(await modifier.UpdateOption(option, req.Value), req.AppId)).IsFailed(out var error)) return error;
             return Res.Ok("路由到中心节点保存成功");
        }

        if ((await modifier.IsConfigExist(req.Key)).IsOk(out var config))
        {
            if ((await SaveHistory(await modifier.UpdateConfig(config, req.Value), req.AppId)).IsFailed(out var error)) return error;
            return Res.Ok("路由到中心节点保存成功");

        }


        //否则调用相应服务修改
        if (Dict.Values.FirstOrDefault(p => p.AppId.Equals(req.AppId)) is {} service)
        {
            if ((await _connector.PostAsync<DtoUpdateConfig, Res<DtoUpdateConfigRes>>(service.AppId, $"{MoConfigurationConventions.DashboardClientConfigUpdate}", req)).IsFailed(out var error, out var data)) return error;

            if ((await SaveHistory(data, req.AppId)).IsFailed(out error)) return error;
            return Res.Ok($"路由到{service.AppId}节点保存成功");
        }

        return $"找不到{req.AppId}所对应的微服务";
    }

    public async Task<Res<List<DtoDomainConfigs>>> WashDomainConfigs(List<DtoDomainConfigs> configs)
    {
        var group = configs.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.ToList());
        var finalDomainConfigs = new List<DtoDomainConfigs>();
        foreach (var item in group)
        {
            var tmp = item.Value.First();
            tmp.Children = item.Value.SelectMany(p => p.Children).DistinctBy(p => p.Name).ToList();
            finalDomainConfigs.Add(tmp);
        }

        return finalDomainConfigs;
    }
}