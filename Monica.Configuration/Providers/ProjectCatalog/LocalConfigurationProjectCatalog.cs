using System.Reflection;
using Monica.Configuration.Abstractions;

namespace Monica.Configuration.Providers.ProjectCatalog;

/// <summary>
/// Local project catalog used when the host exposes only its own configuration data.
/// </summary>
public class LocalConfigurationProjectCatalog : IConfigurationProjectCatalog
{
    private readonly Lazy<string> _currentProjectName = new(ResolveCurrentProjectName);
    private readonly Lazy<string> _currentDomainName;

    /// <summary>
    /// Initializes the local catalog using the current entry assembly information.
    /// </summary>
    public LocalConfigurationProjectCatalog()
    {
        _currentDomainName = new Lazy<string>(() =>
            ConfigurationProjectCatalogConventions.GetDomainName(_currentProjectName.Value));
    }

    /// <inheritdoc />
    public string CurrentDomainName => _currentDomainName.Value;

    /// <inheritdoc />
    public string CurrentAppId => _currentProjectName.Value;

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
}
