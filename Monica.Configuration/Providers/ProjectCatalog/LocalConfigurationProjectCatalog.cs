using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Monica.Configuration.Abstractions;
using Monica.ServiceDiscovery.Abstractions;

namespace Monica.Configuration.Providers.ProjectCatalog;

/// <summary>
/// Local project catalog used when the host exposes only its own configuration data.
/// </summary>
public class LocalConfigurationProjectCatalog : IConfigurationProjectCatalog
{
    private readonly Lazy<string> _currentProjectName = new(ResolveCurrentProjectName);
    private readonly Lazy<string> _currentDomainName;
    private readonly Lazy<string> _currentAppId;

    /// <summary>
    /// Initializes the local catalog using the current entry assembly information.
    /// </summary>
    public LocalConfigurationProjectCatalog(IServiceProvider serviceProvider)
    {
        _currentDomainName = new Lazy<string>(() =>
            ConfigurationProjectCatalogConventions.GetDomainName(_currentProjectName.Value));
        _currentAppId = new Lazy<string>(() => ResolveCurrentAppId(serviceProvider));
    }

    /// <inheritdoc />
    public string CurrentDomainName => _currentDomainName.Value;

    /// <inheritdoc />
    public string CurrentAppId => _currentAppId.Value;

    /// <inheritdoc />
    public string GetDomainName(string projectName)
    {
        return ConfigurationProjectCatalogConventions.GetDomainName(projectName);
    }

    /// <inheritdoc />
    public string GetDomainTitle(string domainName)
    {
        return domainName == "Shared" ? "系统通用" : domainName;
    }

    /// <inheritdoc />
    public string GetProjectDisplayName(string projectName)
    {
        var domainName = GetDomainName(projectName);
        return ConfigurationProjectCatalogConventions.GetProjectDisplayName(projectName, GetDomainTitle(domainName));
    }

    /// <inheritdoc />
    public bool IsCurrentDomain(string projectName)
    {
        return string.Equals(CurrentDomainName, GetDomainName(projectName), StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCurrentProjectName()
    {
        var projectName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            return projectName;
        }

        var friendlyName = AppDomain.CurrentDomain.FriendlyName;
        return friendlyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? friendlyName[..^4]
            : friendlyName;
    }

    private string ResolveCurrentAppId(IServiceProvider serviceProvider)
    {
        var clientInfo = serviceProvider.GetService<IServiceDiscoveryClientInfo>();
        var appId = clientInfo?.GetServiceStatus().AppId;
        return string.IsNullOrWhiteSpace(appId) ? _currentProjectName.Value : appId;
    }
}
