using System.Linq.Expressions;
using System.Reflection;
using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Features.MoMapper;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Modules;



public static class ModuleMapperBuilderExtensions
{
    public static ModuleMapperGuide AddMoModuleMapper(this WebApplicationBuilder builder,
        Action<ModuleMapperOption>? action = null)
    {
        return new ModuleMapperGuide().Register(action);
    }
}

public class ModuleMapper(ModuleMapperOption option) : MoModule<ModuleMapper, ModuleMapperOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.Mapper;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        if (option.DebugMapper)
        {
            //https://github.com/MapsterMapper/Mapster/wiki/Debugging
            TypeAdapterConfig.GlobalSettings.Compiler = exp => exp.CompileWithDebugInfo(
                new ExpressionCompilationOptions()
                {
                    //ThrowOnFailedCompilation = true,
                    EmitFile = true,
                    References = [Assembly.GetAssembly(typeof(Res))!, Assembly.GetAssembly(typeof(Enumerable))!, .. option.DebuggerRelatedAssemblies ?? []]
                });
        }

        Task.Factory.StartNew(() =>
        {
            TypeAdapterConfig.GlobalSettings.Compile();
        }).ContinueWith((t) =>
        {
            Environment.FailFast($"Mapper编译失败，定义有误，请检查。{t.Exception}");
        }, TaskContinuationOptions.OnlyOnFaulted);

        services.AddSingleton(TypeAdapterConfig.GlobalSettings);
        services.AddScoped<IMapper, ServiceMapper>();
        services.AddTransient<IMoMapper, MapsterProviderMoObjectMapper>();
        return Res.Ok();
    }

    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetSwaggerGroupName(), Description = "Mapper相关接口" }
            };
            endpoints.MapGet("/mapper/status", async (HttpResponse response, HttpContext context) =>
            {
                var cards = MapperExtensions.GetInfos();
                var res = new
                {
                    count = cards.Count,
                    cards = cards.Select(x => new
                    {
                        x.SourceType,
                        x.DestinationType,
                        x.MapExpression
                    })
                };
                await context.Response.WriteAsJsonAsync(res);
            }).WithName("获取Mapper状态信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取Mapper状态信息";
                operation.Description = "获取Mapper状态信息";
                operation.Tags = tagGroup;
                return operation;
            });
        });
        return base.ConfigureApplicationBuilder(app);
    }
}

public class ModuleMapperGuide : MoModuleGuide<ModuleMapper, ModuleMapperOption, ModuleMapperGuide>
{


}

