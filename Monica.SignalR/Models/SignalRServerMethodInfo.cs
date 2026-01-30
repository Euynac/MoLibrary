namespace Monica.SignalR.Models;

/// <summary>
/// SignalR服务端组信息
/// </summary>
public class SignalRServerGroupInfo
{

    /// <summary>
    /// 组来源Hub类名
    /// </summary>
    public required string Source { get; set; } 

    /// <summary>
    /// 组Hub路由
    /// </summary>
    public required string Route { get; set; }

    /// <summary>
    /// 组方法列表
    /// </summary>
    public List<SignalRServerMethodInfo> Methods { get; set; } = [];
}


/// <summary>
/// SignalR服务端方法信息
/// </summary>
public class SignalRServerMethodInfo
{
    /// <summary>
    /// 方法描述
    /// </summary>
    public string Desc { get; set; } = string.Empty;

    /// <summary>
    /// 方法名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 方法参数列表
    /// </summary>
    public List<SignalRMethodParameter> Args { get; set; } = [];

}

/// <summary>
/// SignalR方法参数信息
/// </summary>
public class SignalRMethodParameter
{
    /// <summary>
    /// 参数类型名称
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// SignalR连接用户信息
/// </summary>
public class SignalRConnectedUserInfo
{
    /// <summary>
    /// 连接ID
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectionTime { get; set; }

    /// <summary>
    /// 用户Claims信息
    /// </summary>
    public Dictionary<string, string> Claims { get; set; } = [];

    /// <summary>
    /// 是否已认证
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// 用户名称（从Claims中提取）
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 用户ID（从Claims中提取）
    /// </summary>
    public string? UserId { get; set; }
} 