using System.Linq.Expressions;
using System.Reflection;
using ExpressionDebugger;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoMapper;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Tool.MoResponse;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Mapper服务，实现核心业务逻辑
/// </summary>
public class MapperService(ILogger<MapperService> logger)
{
    /// <summary>
    /// 获取Mapper状态信息
    /// </summary>
    /// <returns>Mapper状态信息</returns>
    public async Task<Res<MapperStatusResponse>> GetMapperStatusAsync()
    {
        try
        {
            var cards = MapperExtensions.GetInfos();
            var response = new MapperStatusResponse
            {
                Count = cards.Count,
                MapperInfos = cards.Select(x => new MapperInfo
                {
                    SourceType = x.SourceType ?? "",
                    DestinationType = x.DestinationType ?? "",
                    MapExpression = x.MapExpression ?? ""
                }).ToList()
            };

            logger.LogDebug("成功获取Mapper状态信息，映射数量: {Count}", cards.Count);
            return Res.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取Mapper状态信息失败");
            return Res.Fail($"获取Mapper状态信息失败: {ex.Message}");
        }
    }
}

/// <summary>
/// Mapper状态响应
/// </summary>
public class MapperStatusResponse
{
    public int Count { get; set; }
    public List<MapperInfo> MapperInfos { get; set; } = new();
}

/// <summary>
/// Mapper信息
/// </summary>
public class MapperInfo
{
    public string SourceType { get; set; } = "";
    public string DestinationType { get; set; } = "";
    public string MapExpression { get; set; } = "";
}

public static class ModuleMapperBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Mapper 模块
        /// </summary>
        public static ModuleMapperGuide AddMapper(Action<ModuleMapperOption>? action = null)
        {
            return new ModuleMapperGuide().Register(action);
        }
    }
}

public class ModuleMapper(ModuleMapperOption option) : MoModule<ModuleMapper, ModuleMapperOption, ModuleMapperGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Mapper;
    }

    public override void ConfigureServices(IServiceCollection services)
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
        services.AddScoped<MapperService>();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapGet("/mapper/status", async (HttpResponse response, HttpContext context, MapperService mapperService) =>
            {
                var result = await mapperService.GetMapperStatusAsync();
                if (result.IsFailed(out var error, out var data))
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new { error });
                    return;
                }

                var res = new
                {
                    count = data.Count,
                    cards = data.MapperInfos.Select(x => new
                    {
                        x.SourceType,
                        x.DestinationType,
                        x.MapExpression
                    })
                };
                await context.Response.WriteAsJsonAsync(res);
            })
            .WithName("获取Mapper状态信息")
            .WithTags(tagName)
            .WithSummary("获取Mapper状态信息")
            .WithDescription("获取Mapper状态信息");
        });
    }
}

public class ModuleMapperGuide : MoModuleGuide<ModuleMapper, ModuleMapperOption, ModuleMapperGuide>
{


}


public class ModuleMapperOption : MoModuleOptionWithMinimalApi<ModuleMapper>
{
    /// <summary>
    /// 启用对Mapper进行调试（暂时仅支持手动调试）
    /// </summary>
    public bool DebugMapper { get; set; } = false;

    /// <summary>
    /// 调试需要传入Mapper定义时涉及的基类或扩展方法相关定义的程序集
    /// </summary>
    public Assembly[]? DebuggerRelatedAssemblies { get; set; }
}