namespace MoLibrary.SignalR.Models;

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

    /// <summary>
    /// 方法所属的Hub类型名称
    /// </summary>
    public string Source { get; set; } = string.Empty;
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