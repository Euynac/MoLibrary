namespace Monica.Core.HostedService.Abstractions.Internal;

internal interface IHostedServiceRegistryWriter
{
    bool Register(IMoHostedService service);
}
