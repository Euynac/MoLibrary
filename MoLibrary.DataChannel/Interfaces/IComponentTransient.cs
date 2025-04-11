namespace MoLibrary.DataChannel.Interfaces;

/// <summary>
/// 组件Transient生命周期标记接口
/// 实现此接口的组件将使用Transient生命周期创建
/// 每次使用时会通过ServiceProvider重新解析实例
/// </summary>
public interface IComponentTransient
{
} 