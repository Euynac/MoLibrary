using Monica.Configuration.Abstractions;
using Monica.ServiceDiscovery.Abstractions;

namespace Monica.Configuration.Providers.ProjectCatalog;

/// <summary>
/// Project catalog implementation using service discovery
/// </summary>
public class ServiceDiscoveryConfigurationProjectCatalog : IConfigurationProjectCatalog
{
    private readonly IServiceDiscoveryCatalogProvider _catalogProvider;
    private readonly Lazy<Dictionary<string, string>> _domainTitleCache;
    private readonly Lazy<string> _currentDomainName;
    private readonly Lazy<string> _currentAppId;

    public ServiceDiscoveryConfigurationProjectCatalog(
        IServiceDiscoveryCatalogProvider catalogProvider,
        IServiceDiscoveryClientInfo clientInfo)
    {
        _catalogProvider = catalogProvider;
     
        _domainTitleCache = new Lazy<Dictionary<string, string>>(LoadDomainTitles);
        _currentDomainName = new Lazy<string>(() => clientInfo.GetServiceStatus().DomainName ?? "Unknown");
        _currentAppId = new Lazy<string>(() => clientInfo.GetServiceStatus().ServiceName);
    }

    public string CurrentDomainName => _currentDomainName.Value;

    public string CurrentAppId => _currentAppId.Value;

    public string GetDomainName(string projectName)
    {
        return ConfigurationProjectCatalogConventions.GetDomainName(projectName);
    }

    public string GetDomainTitle(string domainName)
    {
        if (_domainTitleCache.Value.TryGetValue(domainName, out var title))
        {
            return title;
        }

        // Fallback
        return domainName == "Shared" ? "系统通用" : "未命名子域";
    }

    public string GetProjectDisplayName(string projectName)
    {
        var domainName = GetDomainName(projectName);
        var domainTitle = GetDomainTitle(domainName);
        return ConfigurationProjectCatalogConventions.GetProjectDisplayName(projectName, domainTitle);
    }

    public bool IsCurrentDomain(string projectName)
    {
        var domainName = GetDomainName(projectName);
        return CurrentDomainName == domainName;
    }

    private Dictionary<string, string> LoadDomainTitles()
    {
        try
        {
            var domains = _catalogProvider.GetAllDomainsAsync().GetAwaiter().GetResult();
            return domains.ToDictionary(d => d.Name, d => d.DisplayName ?? d.Name);
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
