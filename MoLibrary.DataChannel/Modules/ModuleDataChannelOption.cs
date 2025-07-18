using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.DataChannel.Modules;

/// <summary>
/// 数据通道配置类
/// 用于配置数据通道的全局设置和选项
/// 实现了IMoModuleOptions接口，支持模块化配置
/// </summary>
public class ModuleDataChannelOption : MoModuleControllerOption<ModuleDataChannel>
{
    /// <summary>
    /// 最近异常保留数量
    /// </summary>
    public int RecentExceptionToKeep { get; set; } = 10;

    /// <summary>
    /// 初始化线程数
    /// </summary>
    public int InitThreadCount { get; set; } = 10;
    
}