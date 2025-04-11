namespace MoLibrary.DataChannel.Interfaces;

/// <summary>
/// 管道设置接口
/// 定义用于配置和初始化数据通道管道的入口点
/// 实现此接口的类负责构建和注册所有必要的管道
/// </summary>
public interface ISetupPipeline
{
    /// <summary>
    /// 设置管道
    /// 在此方法中创建、配置并注册所有数据管道
    /// 在应用程序启动期间由框架自动调用
    /// </summary>
    void Setup();
}