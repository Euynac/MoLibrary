using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Models;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Tool.MoResponse;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Dapr.EventBus;
using MoLibrary.Dapr.EventBus.Models;
using MoLibrary.EventBus.Modules;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Dapr.Modules;

public class ModuleDaprEventBus(ModuleDaprEventBusOption option)
    : MoModuleWithDependencies<ModuleDaprEventBus, ModuleDaprEventBusOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.DaprEventBus;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {

        return Res.Ok();
    }
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    public override Res ConfigureApplicationBuilder(IApplicationBuilder app)
    {
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

            var option = context.RequestServices.GetRequiredService<IOptions<DistributedEventBusOptions>>().Value;
            var busOption = context.RequestServices.GetRequiredService<IOptions<ModuleDaprEventBusOption>>().Value;


            originJson ??= [];
            originJson.AddRange(MoSubscription.GetMoSubscriptions(option, busOption));

            context.Response.Body = originalBodyStream;
            await context.Response.WriteAsJsonAsync(originJson, _jsonSerializerOptions);


        });
        app.UseEndpoints(endpoints =>
        {
            var tagGroup = new List<OpenApiTag>
            {
                new() { Name = option.GetSwaggerGroupName(), Description = "Dapr相关接口" }
            };
            var options = app.ApplicationServices.GetRequiredService<IOptions<ModuleDaprEventBusOption>>().Value;

            endpoints.MapPost(options.DaprEventBusCallback, async (HttpResponse response, HttpContext context, [FromServices] ILogger<DaprDistributedEventBus> logger, [FromServices] IGlobalJsonOption jsonOption) =>
            {
                try
                {
                    var body = await JsonDocument.ParseAsync(context.Request.Body);
                    var pubSubName = body.RootElement.GetProperty("pubsubname").GetString();
                    var topic = body.RootElement.GetProperty("topic").GetString();
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
                    logger.LogError(e, "接收Dapr领域事件出现异常");
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

        return base.ConfigureApplicationBuilder(app);
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleEventBusGuide>().Register().SetDistributedEventBusProvider<DaprDistributedEventBus>();
    }
}