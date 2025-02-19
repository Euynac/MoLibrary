using BuildingBlocksPlatform.Configuration.Model;

namespace BuildingBlocksPlatform.Configuration.Interfaces;

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