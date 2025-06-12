using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Features.MoTimekeeper;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.General;

namespace MoLibrary.Core.Modules;


public static class ModuleTimekeeperBuilderExtensions
{
    public static ModuleTimekeeperGuide ConfigModuleTimekeeper(this WebApplicationBuilder builder,
        Action<ModuleTimekeeperOption>? action = null)
    {
        return new ModuleTimekeeperGuide().Register(action);
    }
}

public class ModuleTimekeeper(ModuleTimekeeperOption option)
    : MoModule<ModuleTimekeeper, ModuleTimekeeperOption, ModuleTimekeeperGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Timekeeper;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoTimekeeper, MoTimekeeperManager>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetSwaggerGroupName(), Description = "Timekeeper基础功能" }
            };
            endpoints.MapGet("/timekeeper/status", async (HttpResponse response, HttpContext context) =>
            {
                var res = MoTimekeeperBase.GetStatistics();
                var list = res.OrderByDescending(p => p.Value.Average).Select(p => new
                {
                    name = p.Key,
                    times = p.Value.Times,
                    average = $"{p.Value.Average:0.##}ms",
                    createAt = $"{p.Value.StartTime}",
                    timesEveryMinutes = $"{p.Value.Times / (DateTime.Now - p.Value.StartTime).TotalMinutes:0.##}",
                    averageMemory = $"{p.Value.AverageMemoryBytes?.FormatBytes()}",
                    lastMemory = $"{p.Value.LastMemoryBytes?.FormatBytes()}",
                    lastDuration = $"{p.Value.LastDuration:0.##}ms",
                    lastExecutedTime = $"{p.Value.LastExecutedTime}",
                });
                await response.WriteAsJsonAsync(list);
            }).WithName("获取Timekeeper统计状态").WithOpenApi(operation =>
            {
                operation.Summary = "获取Timekeeper统计状态";
                operation.Description = "获取Timekeeper统计状态";
                operation.Tags = tagGroup;
                return operation;
            });

            endpoints.MapGet("/timekeeper/running", async (HttpResponse response, HttpContext context) =>
            {
                var runningTimekeepers = MoTimekeeperBase.GetRunningTimekeepers();
                var list = runningTimekeepers.OrderByDescending(p => p.Value.CurrentElapsedMs).Select(p => new
                {
                    name = p.Key,
                    content = p.Value.Content,
                    startTime = $"{p.Value.StartTime:yyyy-MM-dd HH:mm:ss}",
                    currentElapsed = $"{p.Value.CurrentElapsedMs}ms",
                    runningDuration = $"{(DateTime.Now - p.Value.StartTime).TotalSeconds:0.##}s"
                });
                await response.WriteAsJsonAsync(list);
            }).WithName("获取当前正在运行的Timekeeper").WithOpenApi(operation =>
            {
                operation.Summary = "获取当前正在运行的Timekeeper";
                operation.Description = "获取当前正在运行的Timekeeper及其经过时间等统计信息";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}

public class ModuleTimekeeperGuide : MoModuleGuide<ModuleTimekeeper, ModuleTimekeeperOption, ModuleTimekeeperGuide>
{


}

public class ModuleTimekeeperOption : MoModuleControllerOption<ModuleTimekeeper>
{
}