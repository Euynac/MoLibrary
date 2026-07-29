namespace Monica.Core.HostedService.Abstractions.Internal;

internal interface IHostedServiceRegistryWriter
{
    void Publish(IReadOnlyList<IMoHostedService> services);
}
