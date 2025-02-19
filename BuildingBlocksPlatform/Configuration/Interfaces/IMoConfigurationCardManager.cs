using BuildingBlocksPlatform.Configuration.Model;

namespace BuildingBlocksPlatform.Configuration.Interfaces;

public interface IMoConfigurationCardManager
{
    /// <summary>
    /// 获取已注册的所有配置卡片信息
    /// </summary>
    /// <returns></returns>
    IEnumerable<MoConfigurationCard> GetHotConfigCards();

    /// <summary>
    /// 获取子域内配置信息
    /// </summary>
    /// <param name="onlyCurDomain">只获取当前子域的配置信息</param>
    /// <returns></returns>
    List<DtoDomainConfigs> GetDomainConfigs(bool? onlyCurDomain = null);
}