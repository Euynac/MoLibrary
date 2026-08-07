using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Execution;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.Results.Abstractions;
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
        public ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> AddChainTracing(Action<ModuleChainTracingOption>? action = null)
        {
            return builder.AddModule<ModuleChainTracing, ModuleChainTracingOption>(action);
        }
    }
}

/// <summary>
/// Chain tracing module.
/// </summary>
public class ModuleChainTracing : MonicaModule<ModuleChainTracingOption>, IWebModule
{
    public override void ConfigureServices(ModuleContext<ModuleChainTracingOption> context)
    {
        var services = context.Services;
        services.AddSingleton<IChainTracing, AsyncLocalChainTracingService>();
    }

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleJsonSerialization, ModuleJsonSerializationOption>();
    }
}

/// <summary>
/// Registration extensions for opt-in chain tracing integrations.
/// </summary>
public static class ModuleChainTracingRegistrationExtensions
{
    /// <summary>
    /// Traces business operations executed through Monica's execution pipeline.
    /// </summary>
    public static ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> UseExecutionTracing(
        this ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> module)
    {
        module.Require<ModuleExecutionPipeline, ModuleExecutionPipelineOption>()
            .AddBehavior(
                typeof(ChainTracingExecutionBehavior<,>),
                ExecutionBehaviorOrder.Diagnostics,
                static descriptor =>
                    descriptor.IsBusinessOperation
                    && typeof(IResultEnvelope).IsAssignableFrom(descriptor.ResultType));
        return module;
    }

    /// <summary>
    /// Traces ASP.NET Core controller actions.
    /// </summary>
    public static ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> UseControllerTracing(
        this ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> module)
    {
        module.Require<ModuleControllers, ModuleControllersOption>()
            .ConfigMvcOption(options => options.Filters.Add<ChainTracingControllerActionFilter>());
        return module;
    }

    /// <summary>
    /// Attaches completed chain-trace metadata to controller result envelopes.
    /// </summary>
    public static ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> AttachControllerTraceMetadata(
        this ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> module)
    {
        module.Require<ModuleControllers, ModuleControllersOption>()
            .ConfigMvcOption(options => options.Filters.Add<ChainTracingResultMetadataActionFilter>());
        return module;
    }

    /// <summary>
    /// Enables EF Core command tracing support.
    /// </summary>
    public static ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> UseDatabaseTracing(this ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> module)
    {
        module.ConfigureServices(context => { context.Services.TryAddScoped<ChainTracingDbCommandInterceptor>(); });
        return module;
    }

    /// <summary>
    /// Enables RPC response tracing middleware.
    /// </summary>
    public static ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> UseRpcTracing(this ModuleRegistration<ModuleChainTracing, ModuleChainTracingOption> module)
    {
        module.Require<ModuleJsonSerialization, ModuleJsonSerializationOption>();
        module.RequireWebHost("RPC chain-tracing middleware must run in an ASP.NET Core request pipeline.");
        module.Require<ModuleResultEnvelope, ModuleResultEnvelopeOption>();
        module.Require<ModuleExceptionHandling, ModuleExceptionHandlingOption>();

        module.ConfigureServices(context => { context.Services.TryAddTransient<RpcChainTracingMiddleware>(); });
        module.ConfigureApplicationBuilder(
            context => { context.ApplicationBuilder.UseMiddleware<RpcChainTracingMiddleware>(); },
            ModuleWebStage.AfterRouting);

        return module;
    }

}

/// <summary>
/// Configuration options for the chain tracing module.
/// </summary>
public class ModuleChainTracingOption : ModuleOptions<ModuleChainTracing>
{
    /// <summary>
    /// Maximum chain depth to prevent unbounded recursion.
    /// </summary>
    public int MaxChainDepth { get; set; } = 50;

    /// <summary>
    /// Maximum node count to avoid unbounded memory growth.
    /// </summary>
    public int MaxNodeCount { get; set; } = 1000;
}
