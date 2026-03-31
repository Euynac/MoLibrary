using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Facades;
using Monica.Profiling.ExecutionTiming.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExecutionTimingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the execution-timing diagnostics module.
        /// </summary>
        public static ModuleExecutionTimingGuide AddExecutionTiming(Action<ModuleExecutionTimingOption>? action = null)
        {
            return new ModuleExecutionTimingGuide().Register(action);
        }
    }
}

/// <summary>
/// Execution-timing module.
/// </summary>
[ModuleKey(EMoModuleKey.ExecutionTiming)]
public class ModuleExecutionTiming(ModuleExecutionTimingOption option)
    : MoModule<ModuleExecutionTiming, ModuleExecutionTimingOption, ModuleExecutionTimingGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ExecutionTimingCollector>();
        services.AddSingleton<IExecutionTimingQuery>(sp => sp.GetRequiredService<ExecutionTimingCollector>());
        services.AddSingleton<IExecutionTimingFactory, ExecutionTimingFactory>();
        services.AddSingleton<ExecutionTimingService>();
        services.AddScoped(sp => new ExecutionTimingFacade(
            sp.GetRequiredService<ExecutionTimingService>(),
            sp.GetRequiredService<ILogger<ExecutionTimingFacade>>()));
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (!Option.ExposeExecutionTimingEndpoints)
        {
            return;
        }

        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();

            endpoints.MapGet(
                    "/execution-timing/statistics",
                    ([FromServices] ExecutionTimingFacade facade) => facade.GetStatistics().GetResponse())
                .WithName("GetExecutionTimingStatistics")
                .WithTags(tagName)
                .WithSummary("Gets aggregated execution-timing statistics.")
                .WithDescription("Returns completed execution-timing statistics ordered by average duration.");

            endpoints.MapGet(
                    "/execution-timing/running",
                    ([FromServices] ExecutionTimingFacade facade) => facade.GetRunningOperations().GetResponse())
                .WithName("GetRunningExecutionTimingOperations")
                .WithTags(tagName)
                .WithSummary("Gets currently running execution-timing operations.")
                .WithDescription("Returns currently running execution-timing operations ordered by elapsed duration.");
        });
    }
}

/// <summary>
/// Configuration guide for the execution-timing module.
/// </summary>
public class ModuleExecutionTimingGuide
    : MoModuleGuide<ModuleExecutionTiming, ModuleExecutionTimingOption, ModuleExecutionTimingGuide>
{
}

/// <summary>
/// Configuration options for the execution-timing module.
/// </summary>
public class ModuleExecutionTimingOption : MoModuleOptionWithMinimalApi<ModuleExecutionTiming>
{
    /// <summary>
    /// Exposes minimal API endpoints for execution-timing diagnostics.
    /// Disable this when the module should provide only DI services for in-process consumers.
    /// </summary>
    public bool ExposeExecutionTimingEndpoints { get; set; } = true;
}
