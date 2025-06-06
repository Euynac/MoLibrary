using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.CancellationManager;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.StateStore.Modules;

/// <summary>
/// 分布式取消令牌管理器模块构建器扩展
/// </summary>
public static class ModuleCancellationManagerBuilderExtensions
{
    /// <summary>
    /// 配置分布式取消令牌管理器模块
    /// </summary>
    /// <param name="builder">Web应用程序构建器</param>
    /// <param name="action">模块配置选项</param>
    /// <returns>返回模块指南实例</returns>
    public static ModuleCancellationManagerGuide ConfigModuleCancellationManager(this WebApplicationBuilder builder,
        Action<ModuleCancellationManagerOption>? action = null)
    {
        return new ModuleCancellationManagerGuide().Register(action);
    }
}

/// <summary>
/// 分布式取消令牌管理器模块
/// 提供跨微服务实例的取消令牌管理功能
/// </summary>
public class ModuleCancellationManager(ModuleCancellationManagerOption option)
    : MoModule<ModuleCancellationManager, ModuleCancellationManagerOption>(option)
{
    /// <summary>
    /// 获取当前模块枚举值
    /// </summary>
    /// <returns>返回取消令牌管理器模块枚举</returns>
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.CancellationManager;
    }

    /// <summary>
    /// 配置服务依赖注入
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>返回配置结果</returns>
    public override Res ConfigureServices(IServiceCollection services)
    {
        // 注册配置选项
        services.Configure<ModuleCancellationManagerOption>(options =>
        {
            options.PollingIntervalMs = Option.PollingIntervalMs;
            options.EnableVerboseLogging = Option.EnableVerboseLogging;
            options.StateTtl = Option.StateTtl;
        });

        // 注册分布式取消令牌管理器服务
        services.AddSingleton<IMoCancellationManager, DefaultDistributedCancellationManager>();

        return Res.Ok();
    }
}

/// <summary>
/// 分布式取消令牌管理器模块指南
/// </summary>
public class ModuleCancellationManagerGuide : MoModuleGuide<ModuleCancellationManager, ModuleCancellationManagerOption,
    ModuleCancellationManagerGuide>
{
}

/// <summary>
/// 分布式取消令牌管理器模块配置选项
/// </summary>
public class ModuleCancellationManagerOption : MoModuleOption<ModuleCancellationManager>
{
    /// <summary>
    /// 轮询间隔（毫秒），默认为1000ms
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用详细日志记录，默认为false
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// 取消令牌状态的TTL（生存时间），默认为24小时
    /// 设置为null表示永不过期
    /// </summary>
    public TimeSpan? StateTtl { get; set; } = TimeSpan.FromHours(24);
}