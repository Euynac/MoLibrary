using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Profiling.Services;
using Monica.Tool.MoResponse;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProfilingBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Profiling 模块
        /// </summary>
        public static ModuleProfilingGuide AddProfiling(Action<ModuleProfilingOption>? action = null)
        {
            return new ModuleProfilingGuide().Register(action);
        }
    }
}

public class ModuleProfiling(ModuleProfilingOption option)
    : MoModule<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Profiling;
    }

    /// <summary>
    ///     配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册性能指标收集器为单例 (维护历史数据)
        services.AddSingleton<ProfilingMetricsCollector>();
    }

    /// <summary>
    ///     配置端点
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            // 获取系统性能信息
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