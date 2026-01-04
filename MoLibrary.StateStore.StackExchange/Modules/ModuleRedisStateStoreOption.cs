using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.StateStore.StackExchange.Modules;

/// <summary>
/// Redis 状态存储模块配置选项
/// </summary>
public class ModuleRedisStateStoreOption : MoModuleOption<ModuleRedisStateStore>
{
    /// <summary>
    /// Redis 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// 键前缀（可选）
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// 默认 TTL（可选）
    /// </summary>
    public TimeSpan? DefaultTTL { get; set; }
}
