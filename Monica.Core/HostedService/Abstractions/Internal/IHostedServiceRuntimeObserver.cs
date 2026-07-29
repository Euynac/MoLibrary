namespace Monica.Core.HostedService.Abstractions.Internal;

/// <summary>
/// Attaches one reversible runtime observer to a Monica hosted-service instance.
/// </summary>
internal interface IHostedServiceRuntimeObserver
{
    IDisposable Observe(IMoHostedService service);
}
