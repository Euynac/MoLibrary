using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MoLibrary.Tool.MoResponse;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

namespace MoLibrary.Core.Features.MoMapper;

public static class ServiceCollectionExtensions
{
    private static bool _hasInit;//TODO 后续转为Module自动判断。注意需要区分开发者调用和内部module调用注册的优先级。另外还需要支持内部module调用设置Option的情况，与开发者设定要合并处理

    public static void AddMoMapper(this IServiceCollection services, Action<MoMapperOption>? action = null)
    {
        if(_hasInit)return;
        _hasInit = true;
        var option = new MoMapperOption();
        action?.Invoke(option);
        
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
    }

    /// <summary>
    /// Mapper状态中间件
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoMapper(this IApplicationBuilder app, string? groupName = "Mapper")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = groupName, Description = "Mapper相关接口" }
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
    }
}


public class MoMapperOption
{
    public ILogger Logger { get; set; } = NullLogger.Instance;
    /// <summary>
    /// 启用对Mapper进行调试（暂时仅支持手动调试）
    /// </summary>
    public bool DebugMapper { get; set; } = false;

    /// <summary>
    /// 调试需要传入Mapper定义时涉及的基类或扩展方法相关定义的程序集
    /// </summary>
    public Assembly[]? DebuggerRelatedAssemblies { get; set; }
}
