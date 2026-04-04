using Monica.DataChannel.Abstractions;

namespace Monica.DataChannel.Services;

/// <summary>
/// Default implementation of <see cref="IDataChannelManager"/>.
/// Uses <see cref="DataChannelCentral"/> to store and manage data channels.
/// </summary>
internal class DataChannelManager : IDataChannelManager
{
    /// <summary>
    /// Gets the data channel instance for the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the data channel.</param>
    /// <returns>The matching data channel instance, or <see langword="null"/> if it does not exist.</returns>
    public DataChannel? Fetch(string id)
    {
        return DataChannelCentral.Channels.GetValueOrDefault(id);
    }

    /// <summary>
    /// Gets all data channel instances that belong to the specified group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the data channel group.</param>
    /// <returns>A read-only list of all data channel instances in the specified group.</returns>
    public IReadOnlyList<DataChannel> FetchGroup(string groupId)
    {
        return DataChannelCentral.Channels.Values.Where(p => p.Pipe.GroupId == groupId).ToList();
    }

    /// <summary>
    /// Gets all registered data channel instances.
    /// </summary>
    /// <returns>A read-only list of all data channel instances.</returns>
    public IReadOnlyList<DataChannel> FetchAll()
    {
        return DataChannelCentral.Channels.Values.ToList();
    }
}
