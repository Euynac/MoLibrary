using BuildingBlocksPlatform.Configuration.Model;

namespace BuildingBlocksPlatform.Configuration.Interfaces;

public interface IMoConfigurationServiceInfo
{
    /// <summary>
    /// 通过项目名获取服务信息
    /// </summary>
    /// <param name="projectName"></param>
    /// <returns></returns>
    public ServiceInfo GetServiceInfo(string projectName);
    /// <summary>
    /// 该项目名是否属于当前领域
    /// </summary>
    /// <param name="projectName"></param>
    /// <returns></returns>
    public bool IsCurrentDomain(string projectName);
}