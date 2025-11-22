using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

public class RegisterCentreServerConnectorDistributedProvider(
    IRegisterCentreClientInfo client, IRegisterCentreServerInvocationConnector connector) :  IRegisterCentreServerConnector
{
    private readonly Lazy<string> _registerCentreAppId = new(client.GetRegisterCentreAppId);
    protected string RegisterCentreAppId => _registerCentreAppId.Value;

    public virtual async Task<Res> Register(ServiceRegisterInfo req)
    {
        if ((await connector.PostAsync<ServiceRegisterInfo, Res>(RegisterCentreAppId, RegisterCentreConventions.ServerCentreRegister, req)).IsFailed(out var error, out var data)) return error;
        return data;
    }

    public virtual async Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req)
    {
        if ((await connector.PostAsync<ServiceHeartbeat, Res<ServiceHeartbeatResponse>>(RegisterCentreAppId, RegisterCentreConventions.ServerCentreHeartbeat, req)).IsFailed(out var error, out var data)) return error;
        return data;
    }

    public virtual async Task<Res<LeaderStatusResponse>> GetLeaderStatus(LeaderStatusRequest req)
    {
        if ((await connector.PostAsync<LeaderStatusRequest, Res<LeaderStatusResponse>>(RegisterCentreAppId, RegisterCentreConventions.ServerCentreLeaderStatus, req)).IsFailed(out var error, out var data)) return error;
        return data;
    }
}