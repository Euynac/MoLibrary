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
    /// <param name="requiresLeaderConfirmation">是否要求返回确认状态，即当Looking时，开始选主后返回确认状态</param>
    Task<Res<LeaderStatusResponse>> GetCurrentLeaderStatusAsync(bool requiresLeaderConfirmation = true);
}

public class ClientSideLeaderService(IRegisterCentreClientInfo client, IRegisterCentreServerConnector connector) : ILeaderService
{
    public Task<Res<LeaderStatusResponse>> GetCurrentLeaderStatusAsync(bool requiresLeaderConfirmation = true)
    {
        var info = client.GetServiceStatus();
        return connector.GetLeaderStatus(new LeaderStatusRequest {AppId = info.AppId, FromClient = info.FromInstance, RequiresLeaderConfirmation = requiresLeaderConfirmation});
    }
}