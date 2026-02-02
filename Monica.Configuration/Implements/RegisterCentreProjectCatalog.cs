using System.Text.RegularExpressions;
using Monica.Configuration.Interfaces;
using Monica.RegisterCentre.Interfaces;

namespace Monica.Configuration.Implements;

/// <summary>
/// Project catalog implementation using RegisterCentre
/// </summary>
public class RegisterCentreProjectCatalog : IMoProjectCatalog
{
    private readonly IRegisterCentreCatalogProvider _catalogProvider;
    private readonly Lazy<Dictionary<string, string>> _domainTitleCache;
    private readonly Lazy<string> _currentDomainName;
    private readonly Lazy<string> _currentAppId;

    // Regex to extract domain from project name (e.g., "FlightService.API" → "Flight")
    private static readonly Regex DomainPattern = new(@"^(.+?)Service\.", RegexOptions.Compiled);

    public RegisterCentreProjectCatalog(
        IRegisterCentreCatalogProvider catalogProvider,
        IRegisterCentreClientInfo clientInfo)
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
        var match = DomainPattern.Match(projectName);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Fallback for shared/platform projects
        return "Shared";
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

        // Generate display name based on project type
        if (projectName.EndsWith(".Domain"))
        {
            return $"{domainTitle}领域层";
        }
        else if (projectName.EndsWith(".Infrastructure"))
        {
            return $"{domainTitle}基础设施层";
        }
        else if (projectName.EndsWith(".API"))
        {
            return $"{domainTitle}服务";
        }

        // Special cases
        if (projectName == "ProtocolPlatform")
        {
            return "全局微服务配置";
        }

        // Default: use project name
        return projectName;
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
