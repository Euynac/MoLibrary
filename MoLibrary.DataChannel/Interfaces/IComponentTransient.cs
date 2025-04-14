namespace MoLibrary.DataChannel.Interfaces;

/// <summary>
/// 组件Transient生命周期标记接口
/// 实现此接口的组件将使用Transient生命周期创建
/// 每次使用时会通过ServiceProvider重新解析实例
/// Dispose和Init方法只会调用一次，线程安全。但是由于实例会被释放，所以注意采用static等方式保持状态
/// </summary>
public interface IComponentTransient
{
} 