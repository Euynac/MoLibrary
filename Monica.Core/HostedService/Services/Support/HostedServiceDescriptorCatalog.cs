using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Monica.Core.HostedService.Services.Support;

/// <summary>
/// Retains the host-owned service collection so startup validation sees registrations added after this module runs.
/// </summary>
internal sealed class HostedServiceDescriptorCatalog(IServiceCollection services)
{
    public IReadOnlyList<ServiceDescriptor> GetDescriptors()
    {
        return services
            .Where(static descriptor => descriptor.ServiceType == typeof(IHostedService))
            .ToArray();
    }
}
