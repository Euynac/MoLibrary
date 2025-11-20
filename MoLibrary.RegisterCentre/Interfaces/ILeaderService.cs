using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

public interface ILeaderService
{
    /// <summary>
    /// 查询当前实例的领导者状态（Leader/Follower/Looking）
    /// </summary>
    Task<Res<LeaderStatusResponse>> GetCurrentLeaderStatusAsync();
}

public class ClientSideLeaderService(IRegisterCentreClient client, IRegisterCentreServerConnector connector) : ILeaderService
{
    public Task<Res<LeaderStatusResponse>> GetCurrentLeaderStatusAsync()
    {
        var info = client.GetServiceStatus();
        return connector.GetLeaderStatus(new LeaderStatusRequest {AppId = info.AppId, FromClient = info.FromInstance});
    }
}