namespace Monica.DataChannel.Abstractions;

/// <summary>
/// Defines operations for retrieving and managing data channels.
/// </summary>
public interface IDataChannelManager
{
    /// <summary>
    /// Gets the data channel instance for the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the data channel.</param>
    /// <returns>The matching data channel instance, or <see langword="null"/> if it does not exist.</returns>
    DataChannel? Fetch(string id);

    /// <summary>
    /// Gets all data channel instances that belong to the specified group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the data channel group.</param>
    /// <returns>A read-only list of all data channel instances in the specified group.</returns>
    IReadOnlyList<DataChannel> FetchGroup(string groupId);

    /// <summary>
    /// Gets all registered data channel instances.
    /// </summary>
    /// <returns>A read-only list of all data channel instances.</returns>
    IReadOnlyList<DataChannel> FetchAll();
}
