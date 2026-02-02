using Monica.Configuration.Model;
using Monica.Configuration.UI.Model;
using Monica.Tool.MoResponse;

namespace Monica.Configuration.UI.Interfaces;

/// <summary>
/// 统一配置管理API接口，支持配置中心和客户端模式
/// </summary>
public interface IMoConfigurationApi
{
    /// <summary>
    /// 获取所有配置状态信息
    /// </summary>
    /// <param name="mode">显示模式（可选）</param>
    /// <param name="onlyCurDomain"></param>
    /// <returns>配置状态列表</returns>
    Task<Res<List<DtoDomainGroup>>> GetConfigsAsync(string? mode = null, bool onlyCurDomain = false);

    /// <summary>
    /// 获取指定配置项状态信息
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appid">应用ID（可选）</param>
    /// <returns>配置项状态</returns>
    Task<Res<DtoOptionItem>> GetOptionItemAsync(string key, string? appid = null);

    /// <summary>
    /// 获取指定配置类状态信息
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appid">应用ID（可选）</param>
    /// <returns>配置类状态</returns>
    Task<Res<DtoConfig>> GetConfigAsync(string key, string? appid = null);

    /// <summary>
    /// 获取配置历史记录
    /// </summary>
    /// <param name="key">配置键（可选）</param>
    /// <param name="appid">应用ID（可选）</param>
    /// <param name="start">开始时间（可选）</param>
    /// <param name="end">结束时间（可选）</param>
    /// <returns>配置历史列表</returns>
    Task<Res<List<DtoOptionHistory>>> GetConfigHistoryAsync(
        string? key = null,
        string? appid = null,
        DateTime? start = null,
        DateTime? end = null);

    /// <summary>
    /// 更新配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    Task<Res<DtoUpdateConfigRes>> UpdateConfigAsync(DtoUpdateConfig request);

    /// <summary>
    /// 回滚配置到指定版本
    /// </summary>
    /// <param name="key">配置键</param>
    /// <param name="appid">应用ID</param>
    /// <param name="version">版本号</param>
    /// <returns>回滚结果</returns>
    Task<Res<DtoUpdateConfigRes>> RollbackConfigAsync(string key, string appid, string version);
}
