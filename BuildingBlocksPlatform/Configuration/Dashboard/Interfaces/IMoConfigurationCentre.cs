using BuildingBlocksPlatform.Configuration.Dashboard.Model;
using BuildingBlocksPlatform.Configuration.Model;
using BuildingBlocksPlatform.SeedWork;

namespace BuildingBlocksPlatform.Configuration.Dashboard.Interfaces;

/// <summary>
/// 管理各个注册了热配置的微服务状态，配置中心
/// </summary>
public interface IMoConfigurationCentre
{
    /// <summary>
    /// 获取所有已注册的服务配置状态信息
    /// </summary>
    /// <returns></returns>
    Task<Res<List<DtoDomainConfigs>>> GetRegisteredServicesConfigsAsync();

    /// <summary>
    /// 回滚配置类配置
    /// </summary>
    /// <returns></returns>
    Task<Res> RollbackConfig(string key, string appid, string version);

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="req"></param>
    /// <returns></returns>
    Task<Res> UpdateConfig(DtoUpdateConfig req);

    /// <summary>
    /// 清洗所有配置
    /// </summary>
    /// <param name="configs"></param>
    /// <returns></returns>
    Task<Res<List<DtoDomainConfigs>>> WashDomainConfigs(List<DtoDomainConfigs> configs);
}

