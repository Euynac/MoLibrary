using MoLibrary.Configuration.Model;

namespace MoLibrary.Configuration.Interfaces;

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