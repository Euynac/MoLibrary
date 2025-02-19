namespace BuildingBlocksPlatform.Authority.Security;

public interface IOurCurrentUser : IMoCurrentUser
{
    /// <summary>
    /// 用户权限信息
    /// </summary>
    string? DefaultBit { get; }
    /// <summary>
    /// 用户当前IP信息（仅从HTTPContext中获取有值）
    /// </summary>
    string? IpAddress { get; }
}

public static class OurClaimTypes
{
    /// <summary>
    /// 接口权限
    /// </summary>
    public static string DefaultBit { get; set; } = "bit";

    /// <summary>
    /// 告警权限
    /// </summary>
    public static string AlarmCodeBit { get; set; } = "alarm";

    /// <summary>
    /// 关键字告警权限
    /// </summary>
    public static string KeywordAlarmCodeBit { get; set; } = "alarm_key";
}