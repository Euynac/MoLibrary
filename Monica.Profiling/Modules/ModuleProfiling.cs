using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Profiling.Services;
using Monica.Core.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProfilingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Profiling module
        /// </summary>
        public static ModuleProfilingGuide AddProfiling(Action<ModuleProfilingOption>? action = null)
        {
            return new ModuleProfilingGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Profiling)]
public class ModuleProfiling(ModuleProfilingOption option)
    : MoModule<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>(option)
{

    /// <summary>
    /// Configuration service
    /// </summary>
    /// <param name="services">Service collection</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the performance indicator collector as a singleton (maintain historical data)
        services.AddSingleton<ProfilingMetricsCollector>();
    }

    /// <summary>
    /// Configure endpoint
    /// </summary>
    /// <param name="app">application builder</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            // Get system performance information
            endpoints.MapGet("/profiling/simple",
                ([FromServices] ProfilingMetricsCollector collector) =>
                {
                    var dataPoint = collector.GetCurrentDataPoint();
                    if (dataPoint == null)
                    {
                        return Res.Fail("性能数据尚未采集，请稍后重试").GetResponse();
                    }

                    return Res.Ok(new
                    {
                        CpuUsage = $"{dataPoint.CpuUsagePercent:0.##}%",
                        MemoryUsage = $"{dataPoint.WorkingSetMB:0.##}MB",
                        GcHeapSize = $"{dataPoint.GcHeapSizeMB:0.##}MB",
                        ThreadCount = dataPoint.ThreadCount,
                        Timestamp = dataPoint.Timestamp
                    }).GetResponse();
                })
            .WithName("获取系统性能信息")
            .WithTags(tagName)
            .WithSummary("获取系统性能信息")
            .WithDescription("获取当前进程的CPU使用率和内存使用情况");
        });
    }
}

public class ModuleProfilingGuide : MoModuleGuide<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>
{
}

public class ModuleProfilingOption : MoModuleOptionWithMinimalApi<ModuleProfiling>
{
}