using Monica.Configuration.Model;
using Monica.Configuration.UI.Model;
using Monica.Tool.Results;

namespace Monica.Configuration.UI.Interfaces;

/// <summary>
/// Abstraction for invoking configuration services.
/// Provides a unified interface for retrieving configuration data,
/// supporting both standalone and distributed deployment modes.
/// </summary>
public interface IConfigurationCentreServiceInvoker
{
    /// <summary>
    /// Retrieves registered services' configuration data.
    /// </summary>
    /// <param name="appIds">List of application IDs to retrieve configurations for</param>
    /// <returns>Aggregated domain configurations from all specified services</returns>
    Task<Res<List<DtoDomainGroup>>> GetRegisteredServicesConfigsAsync(List<string> appIds);

    /// <summary>
    /// Updates configuration on a remote service.
    /// </summary>
    /// <param name="appId">Target application ID</param>
    /// <param name="request">Update configuration request</param>
    /// <returns>Update result with configuration response</returns>
    Task<Res<DtoUpdateConfigRes>> UpdateRemoteConfigAsync(string appId, DtoUpdateConfig request);
}
