using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Framework.Core.Model;
using MoLibrary.Logging;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Core.Extensions;

public static class MoFrameworkCoreMonitorExtensions
{
    private static void InitProjectUnitFactories()
    {
        var type = typeof(ProjectUnit);
        var assembly = type.Assembly;
        var related = type.Namespace;
        assembly.GetTypes()
            .Where(p => p.Namespace == related && p.IsSubclassOf(type) &&
                        p.HasExplicitDefinedStaticConstructor()).Do(p => p.RunStaticConstructor());
    }

    /// <summary>
    /// 注册OurFrameworkCore
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IServiceCollection AddMoFrameworkCoreMonitor(this IServiceCollection services, Action<MonitorOption>? action = null)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        InitProjectUnitFactories();
        var setting = new MonitorOption();
        action?.Invoke(setting);
        ProjectUnit.Option = setting;
        // candidates assemblies
        var candidates = Assembly.GetEntryAssembly()!.GetRelatedAssemblies(setting.RelatedAssemblies);
        
        foreach (var assembly in candidates)
        {
            _ = assembly.GetTypes().ExtractUnitInfo(services).ExtractEnumInfo().ToList();
        }


        ProjectUnitStores.ProjectUnitsByFullName.Values.Do(p => p.DoingConnect());

        stopwatch.Stop();
        GlobalLog.LogInformation($"项目分析耗时：{stopwatch.ElapsedMilliseconds}ms");
        services.AddRequestFilter();

        return services;
    }

    /// <summary>
    /// 配置OurFrameworkCore Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    public static void UseMoFrameworkCoreMonitor(this IApplicationBuilder app)
    {
        app.UseRequestFilter();
    }

    class RequestFilterDto
    {
        public List<string>? Urls { get; set; }
        public bool? Disable { get; set; }
    }


    /// <summary>
    /// 配置OurFrameworkCore Endpoints中间件
    /// </summary>
    /// <param name="app"></param>
    /// <param name="groupName"></param>
    public static void UseEndpointsMoFrameworkCoreMonitor(this IApplicationBuilder app, string? groupName = "Core")
    {
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag> { new() { Name = groupName, Description = "系统框架内置接口" } };

            endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish", async ([FromRoute] string eventKey, [FromServices] IMoDistributedEventBus eventBus,[FromServices] IGlobalJsonOption jsonOption, [FromBody]JsonNode eventContent, HttpResponse response, HttpContext context) =>
            {
                if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is {} e)
                {
                    var json = eventContent.ToString();
                    var eventToPublish = JsonSerializer.Deserialize(json, e.Type, jsonOption.GlobalOptions)!;
                    await eventBus.PublishAsync(e.Type, eventToPublish);
                    return Res.Ok(eventToPublish).AppendMsg($"已发布{eventKey}信息").GetResponse();
                }

                return Res.Fail($"获取{eventKey}相关单元信息失败").GetResponse();

            }).WithName("测试发布领域事件信息").WithOpenApi(operation =>
            {
                operation.Summary = "测试发布领域事件信息";
                operation.Description = "测试发布领域事件信息";
                operation.Tags = tagGroup;
                return operation;
            });
            endpoints.MapPost("/framework/request-filter", async ([FromBody] RequestFilterDto dto, IRequestFilter filter, HttpResponse response, HttpContext context) =>
            {
                if (dto is { Urls: { } urls, Disable: { } disable})
                {
                    foreach (var url in urls)
                    {
                        if (disable)
                        {
                            filter.Disable(url);
                        }
                        else
                        {
                            filter.Enable(url);
                        }
                    }
                }
                return filter.GetDisabledUrls();
            }).WithName("请求过滤中间件").WithOpenApi(operation =>
            {
                operation.Summary = "请求过滤中间件";
                operation.Description = "请求过滤中间件";
                operation.Tags = tagGroup;
                return operation;
            });

      

            endpoints.MapGet("/framework/units", async (IMapper mapper, HttpResponse response, HttpContext context) =>
            {
                return mapper.Map<List<DtoProjectUnit>>(ProjectUnitStores.GetAllUnits());
            }).WithName("获取所有项目单元信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取所有项目单元信息";
                operation.Description = "获取所有项目单元信息";
                operation.Tags = tagGroup;
                return operation;
            });

            endpoints.MapGet("/framework/units/domain-event", async (IMapper mapper, HttpResponse response, HttpContext context) =>
            {
                var events = ProjectUnitStores.GetUnits<UnitDomainEvent>();
                return events.Select(p => new {info = mapper.Map<DtoProjectUnit>(p), structure = p.GetStructure()})
                    .ToList();
            }).WithName("获取项目领域事件信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取项目领域事件信息";
                operation.Description = "获取项目领域事件信息";
                operation.Tags = tagGroup;
                return operation;
            });
            endpoints.MapGet("/framework/enum", async (HttpResponse response, HttpContext context, [FromQuery] string? name = null) =>
            {
                if (name == null)
                {
                    var list = ProjectUnitStores.EnumTypes.GroupBy(g=>g.Value.Assembly.FullName).Select(p => new
                    {
                        from = p.Key,
                        enums = p.ToList().Select(e=>new
                        {
                            name = e.Key,
                            values = e.Value.GetEnumValues().OfType<Enum>().Select((p, i) => new
                            {
                                index = i,
                                name = p.ToString(),
                                description = p.GetDescription(),
                            }).ToList()
                            //values = e.Value.GetEnumValues().OfType<Enum>().Select(p=>p.ToString())
                        }).ToList()
                    }).ToList();

                    await response.WriteAsJsonAsync(Res.Ok(list));
                    return;
                }

                if (ProjectUnitStores.EnumTypes.TryGetValue(name, out var enumType))
                {
                    var list = enumType.GetEnumValues().OfType<Enum>().Select((p, i) => new
                    {
                        index = i,
                        name = p.ToString(),
                        description = p.GetDescription(),
                    }).ToList();

                    await response.WriteAsJsonAsync(Res.Ok(list));
                }
                else
                {
                    await response.WriteAsJsonAsync(Res.Fail("未找到相应的枚举类型"));
                }

            }).WithName("获取项目枚举信息").WithOpenApi(operation =>
            {
                operation.Summary = "获取项目枚举信息";
                operation.Description = "获取项目枚举信息";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }
}