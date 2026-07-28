using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Execution;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Providers.AspNetCore;
using Monica.Framework.ChainTracing.Providers.Execution;
using Monica.Framework.ChainTracing.Providers.EntityFrameworkCore;
using Monica.Framework.ChainTracing.Providers.Rpc;
using Monica.Framework.ChainTracing.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleChainTracingBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the ChainTracing module.
        /// </summary>
        public ModuleChainTracingGuide AddChainTracing(Action<ModuleChainTracingOption>? action = null)
        {
            return builder.AddModule<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>(action);
        }
    }
}

/// <summary>
/// Chain tracing module.
/// </summary>
/// <param name="option">The module options.</param>
[ModuleKey(BuiltInModuleKey.ChainTracing)]
public class ModuleChainTracing(ModuleChainTracingOption option)
    : WebModuleBase<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (!Option.Enabled)
        {
            services.AddSingleton<IChainTracing>(_ => EmptyChainTracingService.Instance);
            return;
        }

        services.AddSingleton<IChainTracing, AsyncLocalChainTracingService>();
    }

    public override void ClaimDependencies()
    {
        if (!Option.Enabled)
        {
            return;
        }

        DependsOnModule<ModuleJsonSerializationGuide>().Register();
        DependsOnModule<ModuleExecutionPipelineGuide>().Register()
            .AddBehavior(
                "framework.chain-tracing",
                typeof(ChainTracingExecutionBehavior<,>),
                ExecutionBehaviorOrder.Diagnostics,
                static descriptor => descriptor.IsBusinessOperation && !descriptor.IsLongRunning);

        if (Option.EnableControllerTracing || Option.EnableAttachToRes)
        {
            DependsOnModule<ModuleControllersGuide>().Register().ConfigMvcOption((options, _) =>
            {
                if (Option.EnableControllerTracing)
                {
                    options.Filters.Add<ChainTracingControllerActionFilter>();
                }

                if (Option.EnableAttachToRes)
                {
                    options.Filters.Add<ChainTracingResultMetadataActionFilter>();
                }
            });
        }
    }
}

/// <summary>
/// Configuration guide for the chain tracing module.
/// </summary>
public class ModuleChainTracingGuide : WebModuleGuide<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>
{
    /// <summary>
    /// Enables EF Core command tracing support.
    /// </summary>
    public ModuleChainTracingGuide UseDatabaseTracing()
    {
        ConfigureServices(context => { context.Services.TryAddScoped<ChainTracingDbCommandInterceptor>(); });
        return this;
    }

    /// <summary>
    /// Enables RPC response tracing middleware.
    /// </summary>
    public ModuleChainTracingGuide UseRpcTracing()
    {
        DependsOnModule<ModuleJsonSerializationGuide>().Register();
        DependsOnModule<ModuleResultEnvelopeGuide>().Register();
        DependsOnModule<ModuleExceptionHandlingGuide>().Register();

        ConfigureServices(context => { context.Services.TryAddTransient<RpcChainTracingMiddleware>(); });
        ConfigureApplicationBuilder(
            context => { context.ApplicationBuilder.UseMiddleware<RpcChainTracingMiddleware>(); },
            ModuleApplicationMiddlewareOrder.AfterUseRouting);

        return this;
    }
}

/// <summary>
/// Configuration options for the chain tracing module.
/// </summary>
public class ModuleChainTracingOption : ModuleOptions<ModuleChainTracing>
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
