using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Core.Results;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;

namespace Monica.Configuration.Providers.ServiceInvocation;

/// <summary>
/// Distributed provider for <see cref="IConfigurationRemoteGateway"/>.
/// Uses <see cref="IServiceInvocationConnector"/> to invoke remote services
/// and aggregate their configuration data.
/// </summary>
public class DistributedConfigurationRemoteGateway(
    IServiceInvocationConnector connector) : IConfigurationRemoteGateway
{
    public async Task<Res<List<ConfigurationDomainGroup>>> GetRegisteredServicesConfigsAsync(List<string> appIds)
    {
        var res = await connector.GetAsync<Res<List<ConfigurationDomainGroup>>>(appIds,
            $"{ConfigurationRoutes.DashboardAllConfigStatus}?onlyCurDomain=true");

        var statusList = res.Values
            .Where(p => p.IsOk() && p.Data != null && p.Data.IsOk() && p.Data.Data != null)
            .SelectMany(p => p.Data!.Data!)
            .ToList();

        return Res.Ok(statusList);
    }

    public async Task<Res<ConfigurationUpdateResult>> UpdateRemoteConfigAsync(string appId, ConfigurationUpdateRequest request)
    {
        var res = await connector.PostAsync<ConfigurationUpdateRequest, Res<ConfigurationUpdateResult>>(appId,
            $"{ConfigurationRoutes.DashboardConfigUpdate}", request);

        if (res.IsFailed(out var error, out var data))
            return error;

        return data;
    }
}
