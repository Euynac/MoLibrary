namespace Monica.Core.HostedService.Abstractions.Internal;

internal interface IHostedServiceCheckpointObserver
{
    void Observe(IMoHostedService service);
}
