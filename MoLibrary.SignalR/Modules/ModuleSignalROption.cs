using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.SignalR.Modules;

/// <summary>
/// SignalR模块配置选项
/// </summary>
public class ModuleSignalROption : MoModuleControllerOption<ModuleSignalR>
{
    /// <summary>
    /// 注册的Hub类型
    /// </summary>
    internal List<MoHubInfo> Hubs { get; set; } = [];

    /// <summary>
    /// 默认AccessToken用于SignalR调试页面
    /// </summary>
    public string? DefaultAccessToken { get; set; }
}

/// <summary>
/// Hub信息记录
/// </summary>
public record MoHubInfo(Type HubType, string HubRoute);