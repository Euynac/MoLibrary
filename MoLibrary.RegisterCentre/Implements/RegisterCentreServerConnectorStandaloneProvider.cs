using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

public class RegisterCentreServerConnectorStandaloneProvider(IRegisterCentreServer server) : IRegisterCentreServerConnector
{
    public Task<Res> Register(ServiceRegisterInfo req)
    {
        return server.Register(req);
    }

    public Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req)
    {
        return server.Heartbeat(req);   
    }

    public Task<Res<LeaderStatusResponse>> GetLeaderStatus(LeaderStatusRequest req)
    {
        return server.GetLeaderStatus(req);
    }
}