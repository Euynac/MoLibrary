namespace MoLibrary.SignalR.Models;

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