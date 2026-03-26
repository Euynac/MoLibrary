using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Support;

public sealed class ServiceDiscoveryDomainColorResolver
{
    private const string DefaultColor = "#666666";
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

                var colors = GenerateDistinctColors(domainNames.Count);
                for (var i = 0; i < domainNames.Count; i++)
                {
                    _domainColors[domainNames[i]] = colors[i];
                }
            }

            return new Dictionary<string, string>(_domainColors, StringComparer.OrdinalIgnoreCase);
        }
    }

    public string GetDomainColor(string? domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return DefaultColor;
        }

        lock (_syncRoot)
        {
            return _domainColors.GetValueOrDefault(domainName, DefaultColor);
        }
    }

    private static List<string> GenerateDistinctColors(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var colors = new List<string>(count);
        var hueStep = 360d / count;

        for (var i = 0; i < count; i++)
        {
            var hue = (i * hueStep) % 360;
            var saturation = 65 + (i % 3) * 15;
            var lightness = 50 + (i % 2) * 10;
            colors.Add($"hsl({hue:F0}, {saturation}%, {lightness}%)");
        }

        return colors;
    }
}
