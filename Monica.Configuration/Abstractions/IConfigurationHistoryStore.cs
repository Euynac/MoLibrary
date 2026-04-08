using Monica.Configuration.Models;
using Monica.Core.Results;

namespace Monica.Configuration.Abstractions;

public interface IConfigurationHistoryStore
{
    /// <summary>
    /// Save this change record
    /// </summary>
    /// <param name="config"></param>
    Task<Res> SaveUpdate(ConfigurationUpdateResult config);

    /// <summary>
    /// Get the change history of the specified Key
    /// </summary>
    /// <returns></returns>
    Task<Res<List<ConfigurationHistoryEntry>>> GetHistory(string key, string appid);

    /// <summary>
    /// Get the change history within a specified range
    /// </summary>
    /// <returns></returns>
    Task<Res<List<ConfigurationHistoryEntry>>> GetHistory(DateTime start, DateTime end);

    /// <summary>
    /// Get the change history of the specified Key and version
    /// </summary>
    /// <returns></returns>
    Task<Res<ConfigurationHistoryEntry>> GetHistory(string key, string appid, string version);
}
