using Monica.Configuration.Model;

namespace Monica.Configuration.Interfaces;

public interface IMoConfigurationCardManager
{
    /// <summary>
    /// Gets all registered configuration cards.
    /// </summary>
    /// <returns></returns>
    IEnumerable<MoConfigurationCard> GetConfigCards();

    /// <summary>
    /// Gets configuration information within the current service.
    /// </summary>
    /// <param name="onlyCurDomain">Whether to only return configurations in the current domain.</param>
    /// <returns></returns>
    List<DtoDomainGroup> GetConfigs(bool onlyCurDomain = false);
}
