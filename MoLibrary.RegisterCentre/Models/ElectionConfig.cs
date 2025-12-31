namespace MoLibrary.RegisterCentre.Models;

/// <summary>
/// Leader 选举配置
/// </summary>
public class ElectionConfig
{
    /// <summary>
    /// 心跳周期（秒），默认 15 秒
    /// </summary>
    public int HeartbeatPeriodSeconds { get; set; } = 15;

    /// <summary>
    /// 注册下线 TTL 倍率，默认 3 倍
    /// </summary>
    public double RegistrationOfflineTTLMultiplier { get; set; } = 3;

    /// <summary>
    /// 注册下线 TTL 附加秒数，默认 1 秒
    /// </summary>
    public int RegistrationOfflineTTLAdditionalSeconds { get; set; } = 1;

    /// <summary>
    /// Leader 下线 TTL 倍率，默认 2 倍
    /// </summary>
    public double LeaderOfflineTTLMultiplier { get; set; } = 2;

    /// <summary>
    /// Leader 下线 TTL 附加秒数，默认 1 秒
    /// </summary>
    public int LeaderOfflineTTLAdditionalSeconds { get; set; } = 1;

    /// <summary>
    /// 挣扎周期（秒），默认 3 秒
    /// </summary>
    public int StrugglePeriodSeconds { get; set; } = 3;

    /// <summary>
    /// 放弃挣扎阈值（秒），必须大于挣扎周期，默认 5 秒
    /// </summary>
    public int GiveUpStruggleThresholdSeconds { get; set; } = 5;

    /// <summary>
    /// 心跳抖动范围（毫秒），默认 1000 毫秒
    /// </summary>
    public int HeartbeatJitterMilliseconds { get; set; } = 1000;

    #region 计算属性

    /// <summary>
    /// 心跳周期
    /// </summary>
    public TimeSpan HeartbeatPeriod => TimeSpan.FromSeconds(HeartbeatPeriodSeconds);

    /// <summary>
    /// 注册下线 TTL
    /// </summary>
    public TimeSpan RegistrationTTL => TimeSpan.FromSeconds(
        HeartbeatPeriodSeconds * RegistrationOfflineTTLMultiplier + RegistrationOfflineTTLAdditionalSeconds);

    /// <summary>
    /// Leader 下线 TTL
    /// </summary>
    public TimeSpan LeaderTTL => TimeSpan.FromSeconds(
        HeartbeatPeriodSeconds * LeaderOfflineTTLMultiplier + LeaderOfflineTTLAdditionalSeconds);

    /// <summary>
    /// 挣扎周期
    /// </summary>
    public TimeSpan StrugglePeriod => TimeSpan.FromSeconds(StrugglePeriodSeconds);

    /// <summary>
    /// 放弃挣扎阈值
    /// </summary>
    public TimeSpan GiveUpStruggleThreshold => TimeSpan.FromSeconds(GiveUpStruggleThresholdSeconds);

    #endregion

    /// <summary>
    /// 验证配置有效性
    /// </summary>
    public void Validate()
    {
        if (HeartbeatPeriodSeconds <= 0)
            throw new ArgumentException("心跳周期必须大于0", nameof(HeartbeatPeriodSeconds));

        if (RegistrationOfflineTTLMultiplier <= 0)
            throw new ArgumentException("注册下线 TTL 倍率必须大于0", nameof(RegistrationOfflineTTLMultiplier));

        if (LeaderOfflineTTLMultiplier <= 0)
            throw new ArgumentException("Leader 下线 TTL 倍率必须大于0", nameof(LeaderOfflineTTLMultiplier));

        if (StrugglePeriodSeconds <= 0)
            throw new ArgumentException("挣扎周期必须大于0", nameof(StrugglePeriodSeconds));

        if (GiveUpStruggleThresholdSeconds <= StrugglePeriodSeconds)
            throw new ArgumentException("放弃挣扎阈值必须大于挣扎周期", nameof(GiveUpStruggleThresholdSeconds));
    }
}
