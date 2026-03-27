using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Abstractions.Internal;

namespace Monica.Core.HostedService.Services;

internal sealed class HostedServiceRegistryInitializer(
    IHostedServiceRegistryWriter registryWriter,
    IHostedServiceCheckpointObserver checkpointObserver,
    ILogger<HostedServiceRegistryInitializer> logger)
{
    public void Initialize(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            foreach (var service in services.GetServices<IHostedService>())
            {
                if (service is not IMoHostedService hostedService)
                {
                    continue;
                }

                if (!registryWriter.Register(hostedService))
                {
                    continue;
                }

                checkpointObserver.Observe(hostedService);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering Monica hosted services.");
        }
    }
}
