using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Core.Features.MoChainTracing;
using Monica.Core.Features.MoChainTracing.Decorators;
using Monica.Core.Features.MoChainTracing.Implementations;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleChainTracingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ChainTracing 模块
        /// </summary>
        public static ModuleChainTracingGuide AddChainTracing(Action<ModuleChainTracingOption>? action = null)
        {
            return new ModuleChainTracingGuide().Register(action);
        }
    }
}

/// <summary>
/// 调用链追踪模块
/// </summary>
/// <param name="option">模块配置选项</param>
public class ModuleChainTracing(ModuleChainTracingOption option)
    : MoModuleWithDependencies<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>(option)
{
    
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ChainTracing;
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