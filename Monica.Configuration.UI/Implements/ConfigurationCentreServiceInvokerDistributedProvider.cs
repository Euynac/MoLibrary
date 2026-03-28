using Monica.Configuration.Model;
using Monica.Configuration.UI.Interfaces;
using Monica.Configuration.UI.Model;
using Monica.ServiceDiscovery.ServiceInvocation.Abstractions;
using Monica.Core.Results;

namespace Monica.Configuration.UI.Implements;

/// <summary>
/// Distributed provider for <see cref="IConfigurationCentreServiceInvoker"/>.
/// Uses <see cref="IServiceInvocationConnector"/> to invoke remote services
/// and aggregate their configuration data.
/// </summary>
public class ConfigurationCentreServiceInvokerDistributedProvider(
    IServiceInvocationConnector connector) : IConfigurationCentreServiceInvoker
{
    public async Task<Res<List<DtoDomainGroup>>> GetRegisteredServicesConfigsAsync(List<string> appIds)
    {
        var res = await connector.GetAsync<Res<List<DtoDomainGroup>>>(appIds,
            $"{MoConfigurationConventions.DashboardAllConfigStatus}?onlyCurDomain=true");

        var statusList = res.Values
            .Where(p => p.IsOk() && p.Data != null && p.Data.IsOk() && p.Data.Data != null)
            .SelectMany(p => p.Data!.Data!)
            .ToList();

        return Res.Ok(statusList);
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateRemoteConfigAsync(string appId, DtoUpdateConfig request)
    {
        var res = await connector.PostAsync<DtoUpdateConfig, Res<DtoUpdateConfigRes>>(appId,
            $"{MoConfigurationConventions.DashboardConfigUpdate}", request);

        if (res.IsFailed(out var error, out var data))
            return error;

        return data;
    }
}
