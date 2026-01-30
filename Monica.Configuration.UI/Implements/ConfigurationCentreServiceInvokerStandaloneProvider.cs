using Monica.Configuration.Model;
using Monica.Configuration.UI.Interfaces;
using Monica.Configuration.UI.Model;
using Monica.Configuration.UI.Services;
using Monica.Tool.MoResponse;

namespace Monica.Configuration.UI.Implements;

/// <summary>
/// Standalone provider for <see cref="IConfigurationCentreServiceInvoker"/>.
/// Uses <see cref="ConfigurationUIService"/> to retrieve local configuration data
/// and update local configurations without making remote service invocations.
/// Suitable for single-instance deployments or development environments.
/// </summary>
public class ConfigurationCentreServiceInvokerStandaloneProvider(
    ConfigurationClientApiProvider api) : IConfigurationCentreServiceInvoker
{
    public async Task<Res<List<DtoDomainConfigs>>> GetRegisteredServicesConfigsAsync(List<string> appIds)
    {
        // In standalone mode, retrieve only the local instance's configuration
        // The appIds parameter is ignored since we can't invoke remote services
        if((await api.GetConfigsAsync()).IsFailed(out var error, out var configs))
            return error;
        return Res.Ok(configs);
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateRemoteConfigAsync(string appId, DtoUpdateConfig request)
    {
        // In standalone mode, "remote" updates are actually local updates
        // The appId parameter is ignored since we can only update the local instance
  
        if ((await api.UpdateConfigAsync(request)).IsFailed(out var error, out var result))
            return error;
        return result;
    }
}
