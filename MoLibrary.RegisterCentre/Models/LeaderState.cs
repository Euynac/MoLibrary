namespace MoLibrary.RegisterCentre.Models;

/// <summary>
/// Leader 状态
/// </summary>
public class LeaderState
{
    /// <summary>
    /// Leader 实例 ID
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// 成为 Leader 的时间
    /// </summary>
    public DateTime BecomeLeaderTime { get; set; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string? ServiceName { get; set; }
}
