using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 领导者服务接口
/// 提供查询当前实例领导者状态的功能
/// </summary>
/// TODO 控制中心多副本支持选主
public interface ILeaderService
{
    /// <summary>
    /// 查询当前实例的领导者状态（Leader/Follower/Looking）
    /// </summary>
    Task<Res<LeaderStatusResponse>> GetCurrentLeaderStatusAsync();
}

public class ClientSideLeaderService(IRegisterCentreClientInfo client, IRegisterCentreServerConnector connector) : ILeaderService
{
    public Task<Res<LeaderStatusResponse>> GetCurrentLeaderStatusAsync()
    {
        var info = client.GetServiceStatus();
        return connector.GetLeaderStatus(new LeaderStatusRequest {AppId = info.AppId, FromClient = info.FromInstance});
    }
}