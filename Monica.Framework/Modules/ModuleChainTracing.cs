using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Logging;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DependencyInjection.DynamicProxy.Models;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Providers.AspNetCore;
using Monica.Framework.ChainTracing.Providers.DynamicProxy;
using Monica.Framework.ChainTracing.Providers.EntityFrameworkCore;
using Monica.Framework.ChainTracing.Providers.MoRpc;
using Monica.Framework.ChainTracing.Services;
using Monica.Tool.Extensions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.RpcClient.Abstractions;

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
    }

    public override void ClaimDependencies()
    {
        if (!Option.Enabled)
        {
            return;
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
}

/// <summary>
/// Configuration guide for the chain tracing module.
/// </summary>
public class ModuleChainTracingGuide : MoModuleGuide<ModuleChainTracing, ModuleChainTracingOption, ModuleChainTracingGuide>
{
    /// <summary>
    /// Enables method-invocation tracing through the DynamicProxy module.
    /// </summary>
    /// <param name="shouldIntercept">Optional predicate that controls which services should be proxied.</param>
    public ModuleChainTracingGuide UseInvocationTracing(
        Func<ProxyBuildContext, bool>? shouldIntercept = null)
    {
        DependsOnModule<ModuleExecutionTimingGuide>().Register();
        DependsOnModule<ModuleDynamicProxyGuide>().Register()
            .AddInterceptor<ChainTracingInvocationInterceptor>(shouldIntercept ?? ShouldTraceInvocation);

        return this;
    }

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
            EMoModuleApplicationMiddlewaresOrder.AfterUseRouting);

        return this;
    }

    private static bool ShouldTraceInvocation(
        ProxyBuildContext context)
    {
        var type = context.ImplementationType;
        if (!type.IsAssignableTo<IApplicationService>() &&
            !type.IsAssignableTo<IDomainService>() &&
            !type.IsSubclassOf(typeof(RpcApi)))
        {
            return false;
        }

        LogManager.For(typeof(ModuleChainTracingGuide))
            .LogDebug("Invocation chain record bind: {Service}", type.GetCleanFullName());

        return true;
    }
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
