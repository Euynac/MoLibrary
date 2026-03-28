using Monica.Configuration.Model;
using Monica.Configuration.UI.Model;
using Monica.Core.Results;

namespace Monica.Configuration.UI.Interfaces;

/// <summary>
/// Unified configuration management API interface, supporting configuration center and client mode
/// </summary>
public interface IMoConfigurationApi
{
    /// <summary>
    /// Get all configuration status information
    /// </summary>
    /// <param name="mode">Display mode (optional)</param>
    /// <param name="onlyCurDomain"></param>
    /// <returns>Configuration status list</returns>
    Task<Res<List<DtoDomainGroup>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false);

    /// <summary>
    /// Get the status information of the specified configuration item
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="appid">Application ID (optional)</param>
    /// <returns>Configuration item status</returns>
    Task<Res<DtoOptionItem>> GetOptionItemAsync(string key, string? appid = null);

    /// <summary>
    /// Get status information of specified configuration class
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="appid">Application ID (optional)</param>
    /// <returns>Configuration class status</returns>
    Task<Res<DtoConfig>> GetConfigAsync(string key, string? appid = null);

    /// <summary>
    /// Get configuration history
    /// </summary>
    /// <param name="key">Configuration key (optional)</param>
    /// <param name="appid">Application ID (optional)</param>
    /// <param name="start">Start time (optional)</param>
    /// <param name="end">End time (optional)</param>
    /// <returns>Configuration history list</returns>
    Task<Res<List<DtoOptionHistory>>> GetConfigHistoryAsync(
        string? key = null,
        string? appid = null,
        DateTime? start = null,
        DateTime? end = null);

    /// <summary>
    /// Update configuration
    /// </summary>
    /// <param name="request">Update request</param>
    /// <returns>Update results</returns>
    Task<Res<DtoUpdateConfigRes>> UpdateConfigAsync(DtoUpdateConfig request);

    /// <summary>
    /// Roll back configuration to specified version
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="appid">Application ID</param>
    /// <param name="version">Version number</param>
    /// <returns>Rollback results</returns>
    Task<Res<DtoUpdateConfigRes>> RollbackConfigAsync(string key, string appid, string version);
}
