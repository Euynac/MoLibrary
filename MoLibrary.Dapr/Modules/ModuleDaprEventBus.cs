using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.Dapr.EventBus;
using MoLibrary.Dapr.EventBus.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Dapr.Modules;

public class ModuleDaprEventBus(ModuleDaprEventBusOption option)
    : MoModuleWithDependencies<ModuleDaprEventBus, ModuleDaprEventBusOption, ModuleDaprEventBusGuide>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprEventBus;
    }

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        //用于解析data_base64等application/cloudevents+json格式的请求
        //必须位于abp的UseConfiguredEndpoints之前。
        app.UseCloudEvents();
        //app.UseCorrelationId(); //（ABP）使得dapr中推送的消息Correlation有值，否则当Json使用IgnoreWhenDefault时会拒绝解析
        //                        //已使用ABP dapr的MapSubscribeHandler方法
       
        app.Use(async (context, next) =>
        {
            if (context.Request.Path != "/dapr/subscribe")
            {
                await next();
                return;
            }

            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            await next();

            responseBody.Seek(0, SeekOrigin.Begin);
            var originalResponse = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);

            if (string.IsNullOrWhiteSpace(originalResponse)) return;

            var originJson = JsonSerializer.Deserialize<List<MoSubscription>>(originalResponse, _jsonSerializerOptions);

            var distributedEventBusOptions = context.RequestServices.GetRequiredService<IOptions<DistributedEventBusOptions>>().Value;
            var daprEventBusOption = context.RequestServices.GetRequiredService<IOptions<ModuleDaprEventBusOption>>().Value;


            originJson ??= [];
            originJson.AddRange(MoSubscription.GetMoSubscriptions(distributedEventBusOptions, daprEventBusOption));

            context.Response.Body = originalBodyStream;
            await context.Response.WriteAsJsonAsync(originJson, _jsonSerializerOptions);


        });
    }
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapSubscribeHandler(); //用于map使用了Topic特性标签的的RESTful handler
        });

       
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetApiGroupName(), Description = "Dapr相关接口" }
            };
            var options = app.ApplicationServices.GetRequiredService<IOptions<ModuleDaprEventBusOption>>().Value;

            endpoints.MapGet("/dapr/subscribe-status", async (HttpResponse response, HttpContext context) =>
            {
                //redirect to /dapr/subscribe
                context.Response.Redirect("/dapr/subscribe");
            }).WithName("获取Dapr边车订阅发布组件状态信息 /dapr/subscribe").WithOpenApi(operation =>
            {
                operation.Summary = "获取Dapr边车订阅发布组件状态信息";
                operation.Description = "获取Dapr边车订阅发布组件状态信息";
                operation.Tags = tagGroup;
                return operation;
            });


            //TODO 优化此方法，目前格式化失败将未能拿到原始数据，难以排错。
            endpoints.MapPost(options.DaprEventBusCallback, async (HttpResponse response, HttpContext context, [FromServices] ILogger<DaprDistributedEventBus> logger, [FromServices] IGlobalJsonOption jsonOption) =>
            {
                var topic = "";
                try
                {
                    var body = await JsonDocument.ParseAsync(context.Request.Body);
                    var pubSubName = body.RootElement.GetProperty("pubsubname").GetString();
                    topic = body.RootElement.GetProperty("topic").GetString();
                    var data = body.RootElement.GetProperty("data").GetRawText();
                    if (string.IsNullOrWhiteSpace(pubSubName) || string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(data))
                    {
                        logger.LogError("Invalid Dapr event request.");
                        //return Results.BadRequest();
                        return Results.Ok();
                    }

                    var distributedEventBus = context.RequestServices.GetRequiredService<DaprDistributedEventBus>();

                    object? eventData = null;
                    Type? eventType = null;
                    try
                    {
                        eventType = distributedEventBus.GetEventType(topic!);
                        eventData = JsonSerializer.Deserialize(data, eventType, jsonOption.GlobalOptions);
                    }
                    catch (JsonException e)
                    {
                        logger.LogError(e, $"领域事件主题{topic}，反序列化失败, type: {eventType?.GetCleanFullName()}, data: {data}");
                        return Results.Ok();
                    }

                    if (eventData is null)
                    {
                        logger.LogError($"Event {topic} Json can not deserialize: {data}");
                        return Results.Ok();
                    }

                    await distributedEventBus.TriggerHandlersAsync(distributedEventBus.GetEventType(topic!), eventData);

                    return Results.Ok();
                }
                catch (Exception e)
                {
                    //TODO 后期需要实现死信队列
                    logger.LogError(e, $"接收Dapr领域事件 [{topic.BeNullIfEmpty() ?? "主题获取失败"}] 出现异常");
                    //return Results.BadRequest();
                    return Results.Ok();
                }

            }).WithName("Dapr接收Event事件").WithOpenApi(operation =>
            {
                operation.Summary = "Dapr接收Event事件";
                operation.Description = "Dapr接收Event事件";
                operation.Tags = tagGroup;
                return operation;
            });
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register().SetDistributedEventBusProvider<DaprDistributedEventBus>();
    }
}