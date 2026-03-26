using System.ComponentModel;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Providers;

/// <summary>
/// Default catalog provider that derives metadata from registered instances.
/// </summary>
public class DefaultServiceDiscoveryCatalogProvider(IRegistrationStateManager? stateManager = null) : IServiceDiscoveryCatalogProvider
{
    /// <summary>
    /// Gets all discovery domains by reading registered instance state from the state store.
    /// </summary>
    public virtual async Task<List<DomainInfo>> GetAllDomainsAsync()
    {
        if (stateManager == null)
            return [];

        try
        {
            var instances = await stateManager.GetAllInstancesAsync();

            var domainNames = instances
                .Where(s => !string.IsNullOrWhiteSpace(s.DomainName))
                .Select(s => s.DomainName!)
                .Distinct()
                .ToList();

            return domainNames.Select(name => new DomainInfo
            {
                Name = name,
                DisplayName = name,
                Description = $"Domain: {name}"
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Gets all predefined services.
    /// </summary>
    public virtual Task<List<PredefinedServiceInfo>> GetPreloadedServicesAsync()
    {
        return Task.FromResult<List<PredefinedServiceInfo>>([]);
    }

    /// <summary>
    /// Builds domain metadata from an enum definition, including flag enums.
    /// </summary>
    public static List<DomainInfo> GenerateDomainsFromEnum<TEnum>()
        where TEnum : struct, Enum
    {
        var domainInfos = new List<DomainInfo>();
        var flagValues = Enum.GetValues<TEnum>();

        foreach (var flagValue in flagValues)
        {
            // Skip the zero value, which typically represents None.
            if (Convert.ToInt32(flagValue) == 0)
            {
                continue;
            }

            var name = flagValue.ToString();
            var displayName = name;

            // Prefer the enum Description attribute when present.
            var field = typeof(TEnum).GetField(name);
            if (field != null)
            {
                var descriptionAttr = field.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault();
                if (descriptionAttr != null)
                {
                    displayName = descriptionAttr.Description;
                }
            }

            domainInfos.Add(new DomainInfo
            {
                Name = name,
                DisplayName = displayName
            });
        }

        return domainInfos;
    }
}
