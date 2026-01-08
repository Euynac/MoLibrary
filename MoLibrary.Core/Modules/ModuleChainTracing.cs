using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Features.MoChainTracing;
using MoLibrary.Core.Features.MoChainTracing.Decorators;
using MoLibrary.Core.Features.MoChainTracing.Implementations;

namespace MoLibrary.Core.Modules;

/// <summary>
/// 调用链追踪模块构建器扩展
/// </summary>
public static class ModuleChainTracingBuilderExtensions
{
    /// <summary>
    /// 配置调用链追踪模块
    /// </summary>
    /// <param name="builder">Web应用程序构建器</param>
    /// <param name="action">配置选项的操作</param>
    /// <returns>调用链追踪模块指南</returns>
    public static ModuleChainTracingGuide ConfigModuleChainTracing(this WebApplicationBuilder builder,
        Action<ModuleChainTracingOption>? action = null)
    {
        return new ModuleChainTracingGuide().Register(action);
    }
}

/// <summary>
/// 调用链追踪模块
/// </summary>
/// <param name="option">模块配置选项</param>
public class ModuleChainTracing(ModuleChainTracingOption option)
    : MoModuleWithDependencies<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>(option)
{
    
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.ChainTracing;
    }

    
    public override void ConfigureServices(IServiceCollection services)
    {
        if (Option.Enabled)
        {
            services.AddSingleton<IMoChainTracing, AsyncLocalMoChainTracing>();
        }
        else
        {
            services.AddSingleton<IMoChainTracing>(_ => EmptyChainTracing.Instance);
        }
    }


    public override void ClaimDependencies()
    {
        if (option.EnableControllerTracing || option.EnableAttachToRes)
        {
            DependsOnModule<ModuleControllersGuide>().Register().ConfigMvcOption((options, _) =>
            {
                if(option.EnableControllerTracing)
                {
                    options.Filters.Add<ChainTracingProviderController>();
                }
                if(option.EnableAttachToRes)
                {
                    options.Filters.Add<ChainTracingAttachingActionFilter>();
                }
            });
        }
    }
}

/// <summary>
/// 调用链追踪模块指南
/// </summary>
public class ModuleChainTracingGuide : MoModuleGuide<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>
{
    
}

/// <summary>
/// 调用链追踪模块配置选项
/// </summary>
public class ModuleChainTracingOption : MoModuleOption<ModuleChainTracing>
{
    /// <summary>
    /// 是否启用调用链追踪
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否启用 Controller Tracing
    /// </summary>
    public bool EnableControllerTracing { get; set; } = true;

    /// <summary>
    /// 是否启用将调用链信息附加到响应中
    /// </summary>
    public bool EnableAttachToRes { get; set; } = true;

    /// <summary>
    /// 最大调用链深度（防止无限递归）
    /// </summary>
    public int MaxChainDepth { get; set; } = 50;

    /// <summary>
    /// 最大节点数量（防止内存泄漏）
    /// </summary>
    public int MaxNodeCount { get; set; } = 1000;
}