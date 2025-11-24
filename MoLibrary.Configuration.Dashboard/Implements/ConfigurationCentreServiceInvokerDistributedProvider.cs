using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Model;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Implements;

/// <summary>
/// Distributed provider for <see cref="IConfigurationCentreServiceInvoker"/>.
/// Uses <see cref="IRegisterCentreServerInvocationConnector"/> to invoke remote services
/// and aggregate their configuration data.
/// </summary>
public class ConfigurationCentreServiceInvokerDistributedProvider(
    IRegisterCentreServerInvocationConnector connector) : IConfigurationCentreServiceInvoker
{
    public async Task<Res<List<DtoDomainConfigs>>> GetRegisteredServicesConfigsAsync(List<string> appIds)
    {
        var res = await connector.GetAsync<Res<List<DtoDomainConfigs>>>(appIds,
            $"{MoConfigurationConventions.GetConfigStatus}?onlyCurDomain=true");

        var statusList = res.Values
            .Where(p => p.IsOk() && p.Data != null && p.Data.IsOk() && p.Data.Data != null)
            .SelectMany(p => p.Data!.Data!)
            .ToList();

        return Res.Ok(statusList);
    }

    public async Task<Res<DtoUpdateConfigRes>> UpdateRemoteConfigAsync(string appId, DtoUpdateConfig request)
    {
        var res = await connector.PostAsync<DtoUpdateConfig, Res<DtoUpdateConfigRes>>(appId,
            $"{MoConfigurationConventions.DashboardClientConfigUpdate}", request);

        if (res.IsFailed(out var error, out var data))
            return error;

        return data;
    }
}
