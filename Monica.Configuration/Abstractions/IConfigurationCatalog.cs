using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Abstractions;

public interface IConfigurationCatalog
{
    /// <summary>
    /// Gets all registered configuration cards.
    /// </summary>
    /// <returns></returns>
    IEnumerable<ConfigurationRegistration> GetConfigCards();

    /// <summary>
    /// Gets configuration information within the current service.
    /// </summary>
    /// <param name="onlyCurDomain">Whether to only return configurations in the current domain.</param>
    /// <returns></returns>
    List<ConfigurationDomainGroup> GetConfigs(bool onlyCurDomain = false);
}
