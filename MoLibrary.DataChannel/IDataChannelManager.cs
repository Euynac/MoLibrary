namespace MoLibrary.DataChannel;

/// <summary>
/// 数据通道管理器接口
/// 提供用于获取和管理数据通道的方法
/// </summary>
public interface IDataChannelManager
{
    /// <summary>
    /// 获取指定ID的数据通道实例
    /// </summary>
    /// <param name="id">数据通道的唯一标识符</param>
    /// <returns>指定ID的数据通道实例，如果不存在则返回null</returns>
    DataChannel? Fetch(string id);
    
    /// <summary>
    /// 获取指定组ID的所有数据通道实例列表
    /// </summary>
    /// <param name="groupId">数据通道组的唯一标识符</param>
    /// <returns>属于指定组的所有数据通道实例的只读列表</returns>
    IReadOnlyList<DataChannel> FetchGroup(string groupId);
    
    /// <summary>
    /// 获取所有已注册的数据通道实例
    /// </summary>
    /// <returns>所有数据通道实例的只读列表</returns>
    IReadOnlyList<DataChannel> FetchAll();
}

/// <summary>
/// 数据通道管理器的默认实现
/// 使用DataChannelCentral来存储和管理数据通道
/// </summary>
public class DataChannelManager : IDataChannelManager
{
    /// <summary>
    /// 获取指定ID的数据通道实例
    /// </summary>
    /// <param name="id">数据通道的唯一标识符</param>
    /// <returns>指定ID的数据通道实例，如果不存在则返回null</returns>
    public DataChannel? Fetch(string id)
    {
        return DataChannelCentral.Channels.GetValueOrDefault(id);
    }

    /// <summary>
    /// 获取指定组ID的所有数据通道实例列表
    /// </summary>
    /// <param name="groupId">数据通道组的唯一标识符</param>
    /// <returns>属于指定组的所有数据通道实例的只读列表</returns>
    public IReadOnlyList<DataChannel> FetchGroup(string groupId)
    {
        //TODO 性能优化
        return DataChannelCentral.Channels.Values.Where(p => p.Pipe.GroupId == groupId).ToList();
    }

    /// <summary>
    /// 获取所有已注册的数据通道实例
    /// </summary>
    /// <returns>所有数据通道实例的只读列表</returns>
    public IReadOnlyList<DataChannel> FetchAll()
    {
        return DataChannelCentral.Channels.Values.ToList();
    }
}
