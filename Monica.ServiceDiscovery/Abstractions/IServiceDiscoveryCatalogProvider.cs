using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Abstractions;

/// <summary>
/// Provides predefined service discovery catalog metadata.
/// </summary>
public interface IServiceDiscoveryCatalogProvider
{
    /// <summary>
    /// Returns every known discovery domain.
    /// </summary>
    Task<List<DomainInfo>> GetAllDomainsAsync();
    
    /// <summary>
    /// Returns predefined services that may exist even without active registrations.
    /// </summary>
    Task<List<PredefinedServiceInfo>> GetPreloadedServicesAsync();
}
