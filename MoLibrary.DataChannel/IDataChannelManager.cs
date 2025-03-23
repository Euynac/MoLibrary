namespace MoLibrary.DataChannel;

public interface IDataChannelManager
{
    /// <summary>
    /// 获取消息通路实例
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    DataChannel? Fetch(string id);
    /// <summary>
    /// 获取消息通路组实例列表
    /// </summary>
    /// <param name="groupId"></param>
    /// <returns></returns>
    IReadOnlyList<DataChannel> FetchGroup(string groupId);
    /// <summary>
    /// 获取所有消息通路实例
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<DataChannel> FetchAll();
}

public class DataChannelManager : IDataChannelManager
{
    public DataChannel? Fetch(string id)
    {
        return DataChannelCentral.Channels.GetValueOrDefault(id);
    }

    public IReadOnlyList<DataChannel> FetchGroup(string groupId)
    {
        //TODO 性能优化
        return DataChannelCentral.Channels.Values.Where(p => p.Pipe.GroupId == groupId).ToList();
    }

    public IReadOnlyList<DataChannel> FetchAll()
    {
        return DataChannelCentral.Channels.Values.ToList();
    }
}
