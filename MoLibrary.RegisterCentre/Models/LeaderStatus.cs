namespace MoLibrary.RegisterCentre.Models;

/// <summary>
/// 领导者状态枚举
/// </summary>
public enum LeaderStatus
{
    /// <summary>
    /// 当前实例是领导者
    /// </summary>
    Leader,

    /// <summary>
    /// 当前实例是跟随者（其他实例是领导者）
    /// </summary>
    Follower,

    /// <summary>
    /// 正在寻找领导者（当前没有领导者）
    /// </summary>
    Looking
}
/// <summary>
/// 领导者状态查询请求
/// </summary>
public class LeaderStatusRequest
{
    /// <summary>
    /// 应用ID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 来源客户端标识（用于标识具体实例）
    /// </summary>
    public string? FromClient { get; set; }
}

/// <summary>
/// 领导者状态查询响应
/// </summary>
public class LeaderStatusResponse
{
    /// <summary>
    /// 当前实例的领导者状态
    /// </summary>
    public LeaderStatus Status { get; set; }

    /// <summary>
    /// 当前领导者的实例ID（如果存在）
    /// </summary>
    public string? LeaderInstanceId { get; set; }

    /// <summary>
    /// 领导者注册时间（如果存在）
    /// </summary>
    public DateTime? LeaderRegistrationTime { get; set; }

    /// <summary>
    /// 当前运行中的实例总数
    /// </summary>
    public int RunningInstanceCount { get; set; }

    /// <summary>
    /// 附加消息
    /// </summary>
    public string? Message { get; set; }

    public override string ToString()
    {
        return $"Status: {Status}, LeaderInstanceId: {LeaderInstanceId}, LeaderRegistrationTime: {LeaderRegistrationTime}, RunningInstanceCount: {RunningInstanceCount}, Message: {Message}";
    }
}
