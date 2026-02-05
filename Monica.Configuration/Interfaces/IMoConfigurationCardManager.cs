using Monica.Configuration.Model;

namespace Monica.Configuration.Interfaces;

public interface IMoConfigurationCardManager
{
    /// <summary>
    /// 获取已注册的所有配置卡片信息
    /// </summary>
    /// <returns></returns>
    IEnumerable<MoConfigurationCard> GetConfigCards();

    /// <summary>
    /// 获取当前服务内配置信息
    /// </summary>
    /// <param name="onlyCurDomain">只获取当前子域的配置信息</param>
    /// <returns></returns>
    List<DtoDomainGroup> GetConfigs(bool onlyCurDomain = false);
}