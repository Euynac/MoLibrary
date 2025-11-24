using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Dashboard.Services;
using MoLibrary.Configuration.Model;
using MoLibrary.Configuration.Services;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Implements;

/// <summary>
/// Standalone provider for <see cref="IConfigurationCentreServiceInvoker"/>.
/// Uses <see cref="ModuleConfigurationService"/> to retrieve local configuration data
/// and <see cref="ConfigurationClientService"/> to update local configurations
/// without making remote service invocations.
/// Suitable for single-instance deployments or development environments.
/// </summary>
public class ConfigurationCentreServiceInvokerStandaloneProvider(
    ModuleConfigurationService configurationService,
    ConfigurationClientService clientService) : IConfigurationCentreServiceInvoker
{
    public async Task<Res<List<DtoDomainConfigs>>> GetRegisteredServicesConfigsAsync(List<string> appIds)
    {
        // In standalone mode, retrieve only the local instance's configuration
        // The appIds parameter is ignored since we can't invoke remote services
        var result = await configurationService.GetConfigStatusAsync(onlyCurDomain: true);

        if (result.IsFailed(out var error, out var data))
            return error;

        return Res.Ok(data);
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateRemoteConfigAsync(string appId, DtoUpdateConfig request)
    {
        // In standalone mode, "remote" updates are actually local updates
        // The appId parameter is ignored since we can only update the local instance
        // Use ConfigurationClientService to update the local configuration
        var result = await clientService.UpdateConfigAsync(request);

        if (result.IsFailed(out var error, out var data))
            return error;

        return Res.Ok(data);
    }
}
