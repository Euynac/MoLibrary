using Monica.Configuration.Model;

namespace Monica.Configuration.Interfaces;

public interface IMoConfigurationServiceInfo
{
    /// <summary>
    /// 通过项目名获取服务信息
    /// </summary>
    /// <param name="projectName"></param>
    /// <returns></returns>
    ServiceInfo GetServiceInfo(string projectName);
    /// <summary>
    /// 该项目名是否属于当前领域
    /// </summary>
    /// <param name="projectName"></param>
    /// <returns></returns>
    bool IsCurrentDomain(string projectName);
}