using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Framework.Features.MoProfiling;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Modules;


public static class ModuleProfilingBuilderExtensions
{
    public static ModuleProfilingGuide ConfigModuleProfiling(this WebApplicationBuilder builder,
        Action<ModuleProfilingOption>? action = null)
    {
        return new ModuleProfilingGuide().Register(action);
    }
}

public class ModuleProfiling(ModuleProfilingOption option)
    : MoModule<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Profiling;
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        // 注册MoProfiling服务为单例
        services.AddSingleton<IMoProfiling, MoProfiling>();
    }

    /// <summary>
    /// 配置端点
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = option.GetApiGroupName(), Description = "程序性能监测接口" } };

            // 获取系统性能信息
            endpoints.MapGet("/profiling/simple", async ([FromServices] IMoProfiling profiling, HttpResponse response, HttpContext context) =>
            {
                try
                {
                    var cpuUsage = await profiling.GetCpuUsageAsync();
                    var memoryUsage = await profiling.GetMemoryUsageAsync();
                    return Res.Ok(new
                    {
                        CpuUsage = cpuUsage,
                        MemoryUsage = $"{memoryUsage:0.##}MB"
                    }).GetResponse();
                }
                catch (Exception ex)
                {
                    return Res.Fail($"获取系统性能信息失败: {ex.Message}").GetResponse();
                }
            }).WithName("获取系统性能信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取系统性能信息";
                operation.Description = "获取当前进程的CPU使用率和内存使用情况";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}

public class ModuleProfilingGuide : MoModuleGuide<ModuleProfiling, ModuleProfilingOption, ModuleProfilingGuide>
{
}

public class ModuleProfilingOption : MoModuleControllerOption<ModuleProfiling>
{
}