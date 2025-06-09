using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.StateStore.CancellationManager;

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
    : MoModuleWithDependencies<ModuleCancellationManager, ModuleCancellationManagerOption, ModuleCancellationManagerGuide>(option)
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
    public override void ConfigureServices(IServiceCollection services)
    {
        // 根据配置选择合适的实现
        if (!Option.UseDistributed)
        {
            // 注册内存版取消令牌管理器服务
            services.AddSingleton<IMoCancellationManager, InMemoryCancellationManager>();
        }
        else
        {
            // 注册分布式取消令牌管理器服务
            services.AddSingleton<IMoCancellationManager>((serviceProvider) =>
            {
                var stateStore = serviceProvider.GetRequiredKeyedService<IMoStateStore>(nameof(ModuleCancellationManager));
                return ActivatorUtilities.CreateInstance<DistributedCancellationManager>(serviceProvider, stateStore);
            });
        }
    }

    public override void ClaimDependencies()
    {
        if (Option.UseDistributed)
        {
            DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedStateStore(nameof(ModuleCancellationManager), true);
        }
    }
}

/// <summary>
/// 分布式取消令牌管理器模块指南
/// </summary>
public class ModuleCancellationManagerGuide : MoModuleGuide<ModuleCancellationManager, ModuleCancellationManagerOption,
    ModuleCancellationManagerGuide>
{
    /// <summary>
    /// 添加指定键的取消令牌管理器
    /// </summary>
    /// <param name="key">服务键</param>
    /// <param name="useDistributed">是否使用内存实现，默认为false</param>
    /// <returns>返回当前模块指南实例以支持链式调用</returns>
    public ModuleCancellationManagerGuide AddKeyedCancellationManager(string key, bool useDistributed = false)
    {
        if (useDistributed)
        {
            // 使用分布式实现，需要依赖StateStore
            DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedStateStore(key, true);
        }
        
        ConfigureServices(context =>
        {
            context.Services.AddKeyedSingleton<IMoCancellationManager>(key, (serviceProvider, _) =>
            {
                if (!useDistributed)
                {
                    // 使用内存实现
                    return ActivatorUtilities.CreateInstance<InMemoryCancellationManager>(serviceProvider);
                }
                else
                {
                    // 使用分布式实现
                    var stateStore = serviceProvider.GetRequiredKeyedService<IMoStateStore>(key);
                    return ActivatorUtilities.CreateInstance<DistributedCancellationManager>(serviceProvider, stateStore);
                }
            });
        });
        return this;
    }


}

/// <summary>
/// 分布式取消令牌管理器模块配置选项
/// </summary>
public class ModuleCancellationManagerOption : MoModuleOption<ModuleCancellationManager>
{
    /// <summary>
    /// 是否使用内存实现，默认为false（使用分布式实现）
    /// </summary>
    public bool UseDistributed { get; set; } = false;

    /// <summary>
    /// 轮询间隔（毫秒），默认为1000ms
    /// 仅在使用分布式实现时有效
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用详细日志记录，默认为false
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// 取消令牌状态的TTL（生存时间），默认为24小时
    /// 设置为null表示永不过期
    /// 仅在使用分布式实现时有效
    /// </summary>
    public TimeSpan? StateTtl { get; set; } = TimeSpan.FromHours(24);

}