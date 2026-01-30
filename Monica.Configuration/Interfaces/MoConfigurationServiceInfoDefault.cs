using Monica.Configuration.Model;

namespace Monica.Configuration.Interfaces;

public class MoConfigurationServiceInfoDefault : IMoConfigurationServiceInfo
{
    public ServiceInfo GetServiceInfo(string projectName)
    {
        return new ServiceInfo();
    }

    public bool IsCurrentDomain(string projectName)
    {
        return false;
    }
}