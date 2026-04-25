using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Support;

public sealed class ServiceDiscoveryDomainColorResolver
{
    private static readonly string[] DomainRoles =
    [
        "primary",
        "secondary",
        "tertiary",
        "info",
        "success",
        "warning"
    ];

    private const string DefaultColorRole = "default";
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, string> _domainColors = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _cachedDomainNames = [];

    public IReadOnlyDictionary<string, string> AssignColors(IEnumerable<DomainInfo> domains)
    {
        var domainNames = domains
            .Select(domain => domain.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_syncRoot)
        {
            if (!_cachedDomainNames.SequenceEqual(domainNames, StringComparer.OrdinalIgnoreCase))
            {
                _cachedDomainNames = domainNames;
                _domainColors.Clear();

                for (var i = 0; i < domainNames.Count; i++)
                {
                    _domainColors[domainNames[i]] = DomainRoles[i % DomainRoles.Length];
                }
            }

            return new Dictionary<string, string>(_domainColors, StringComparer.OrdinalIgnoreCase);
        }
    }

    public string GetDomainColorRole(string? domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return DefaultColorRole;
        }

        lock (_syncRoot)
        {
            return _domainColors.GetValueOrDefault(domainName, DefaultColorRole);
        }
    }
}
