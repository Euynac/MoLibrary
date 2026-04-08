using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services;
using Monica.Core.Results;

namespace Monica.Configuration.Providers.ServiceInvocation;

/// <summary>
/// Standalone provider for <see cref="IConfigurationRemoteGateway"/>.
/// Uses <see cref="LocalConfigurationManagementApi"/> to retrieve local configuration data
/// and update local configurations without making remote service invocations.
/// Suitable for single-instance deployments or development environments.
/// </summary>
public class StandaloneConfigurationRemoteGateway(
    LocalConfigurationManagementApi api) : IConfigurationRemoteGateway
{
    public async Task<Res<List<ConfigurationDomainGroup>>> GetRegisteredServicesConfigsAsync(List<string> appIds)
    {
        // In standalone mode, retrieve only the local instance's configuration
        // The appIds parameter is ignored since we can't invoke remote services
        if((await api.GetConfigsAsync()).IsFailed(out var error, out var configs))
            return error;
        return Res.Ok(configs);
    }

    public async Task<Res<ConfigurationUpdateResult>> UpdateRemoteConfigAsync(string appId, ConfigurationUpdateRequest request)
    {
        // In standalone mode, "remote" updates are actually local updates
        // The appId parameter is ignored since we can only update the local instance
  
        if ((await api.UpdateConfigAsync(request)).IsFailed(out var error, out var result))
            return error;
        return result;
    }
}
