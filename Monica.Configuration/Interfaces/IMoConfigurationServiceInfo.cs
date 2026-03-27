using Monica.Configuration.Model;

namespace Monica.Configuration.Interfaces;

public interface IMoConfigurationServiceInfo
{
    /// <summary>
    /// Gets service info by project name.
    /// </summary>
    /// <param name="projectName"></param>
    /// <returns></returns>
    ServiceInfo GetServiceInfo(string projectName);
    /// <summary>
    /// Determines whether the project belongs to the current domain.
    /// </summary>
    /// <param name="projectName"></param>
    /// <returns></returns>
    bool IsCurrentDomain(string projectName);
}
