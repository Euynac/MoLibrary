using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Providers.AspNetCore;
using Monica.Framework.ChainTracing.Providers.DynamicProxy;
using Monica.Framework.ChainTracing.Providers.EntityFrameworkCore;
using Monica.Framework.ChainTracing.Providers.MoRpc;
using Monica.Framework.ChainTracing.Services;

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
[ModuleKey(EMoModuleKey.ChainTracing)]
public class ModuleChainTracing(ModuleChainTracingOption option)
    : MoModule<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        if (!Option.Enabled)
        {
            services.AddSingleton<IChainTracing>(_ => EmptyChainTracingService.Instance);
            return;
        }

        services.AddSingleton<IChainTracing, AsyncLocalChainTracingService>();

        if (Option.EnableInvocationTracing)
        {
            services.AddTransient<ChainTracingInvocationInterceptor>();
        }

        if (Option.EnableDatabaseTracing)
        {
            services.AddScoped<ChainTracingDbCommandInterceptor>();
        }

        if (Option.EnableMoRpcTracing)
        {
            services.AddTransient<MoRpcChainTracingMiddleware>();
        }
    }

    public override void ClaimDependencies()
    {
        if (!Option.Enabled)
        {
            return;
        }

        DependsOnModule<ModuleJsonSerializationGuide>().Register();

        if (Option.EnableInvocationTracing)
        {
            DependsOnModule<ModuleDynamicProxyGuide>().Register();
            DependsOnModule<ModuleTimekeeperGuide>().Register();
        }

        if (Option.EnableMoRpcTracing)
        {
            DependsOnModule<ModuleResultEnvelopeGuide>().Register();
            DependsOnModule<ModuleExceptionHandlingGuide>().Register();
        }

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

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        if (Option.Enabled && Option.EnableMoRpcTracing)
        {
            app.UseMiddleware<MoRpcChainTracingMiddleware>();
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
    /// Enables method-invocation tracing through the dynamic-proxy integration.
    /// </summary>
    public bool EnableInvocationTracing { get; set; }

    /// <summary>
    /// Enables EF Core command tracing support.
    /// </summary>
    public bool EnableDatabaseTracing { get; set; }

    /// <summary>
    /// Enables MoRpc response tracing middleware.
    /// </summary>
    public bool EnableMoRpcTracing { get; set; }

    /// <summary>
    /// Maximum chain depth to prevent unbounded recursion.
    /// </summary>
    public int MaxChainDepth { get; set; } = 50;

    /// <summary>
    /// Maximum node count to avoid unbounded memory growth.
    /// </summary>
    public int MaxNodeCount { get; set; } = 1000;
}
