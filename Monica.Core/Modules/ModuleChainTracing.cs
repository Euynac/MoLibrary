using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
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
        /// Configures the ChainTracing module.
        /// </summary>
        public static ModuleChainTracingGuide AddChainTracing(Action<ModuleChainTracingOption>? action = null)
        {
            return new ModuleChainTracingGuide().Register(action);
        }
    }
}

/// <summary>
/// Chain tracing module.
/// </summary>
/// <param name="option">The module options.</param>
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
/// Configuration guide for the chain tracing module.
/// </summary>
public class ModuleChainTracingGuide : MoModuleGuide<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>
{
    
}

/// <summary>
/// Configuration options for the chain tracing module.
/// </summary>
public class ModuleChainTracingOption : MoModuleOption<ModuleChainTracing>
{
    /// <summary>
    /// Enables chain tracing.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enables controller tracing.
    /// </summary>
    public bool EnableControllerTracing { get; set; } = true;

    /// <summary>
    /// Enables attaching chain tracing information to responses.
    /// </summary>
    public bool EnableAttachToRes { get; set; } = true;

    /// <summary>
    /// Maximum chain depth to prevent unbounded recursion.
    /// </summary>
    public int MaxChainDepth { get; set; } = 50;

    /// <summary>
    /// Maximum node count to avoid unbounded memory growth.
    /// </summary>
    public int MaxNodeCount { get; set; } = 1000;
}
