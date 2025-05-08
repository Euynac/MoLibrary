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
using MoLibrary.EventBus.Abstractions;
using MoLibrary.EventBus.Attributes;
using MoLibrary.EventBus.Dapr;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.EventBus.Modules;

public class ModuleEventBus(ModuleEventBusOption option) : MoModule<ModuleEventBus, ModuleEventBusOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.EventBus;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoLocalEventBus, LocalEventBus>();
        services.AddSingleton<IMoDistributedEventBus, DaprDistributedEventBus>();
        services.AddSingleton<DaprDistributedEventBus>();
        services.AddSingleton<LocalEventBus>();
        services.AddSingleton<IEventHandlerInvoker, EventHandlerInvoker>();
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
            var busOption = context.RequestServices.GetRequiredService<IOptions<ModuleEventBusOption>>().Value;


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
            var options = app.ApplicationServices.GetRequiredService<IOptions<ModuleEventBusOption>>().Value;

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
}
/// <summary>
/// This class defines subscribe endpoint response
/// </summary>
file class MoSubscription
{
    public static IEnumerable<MoSubscription> GetMoSubscriptions(DistributedEventBusOptions option,
        ModuleEventBusOption busOption)
    {
        var result = new List<MoSubscription>();
        foreach (var handler in option.Handlers)
        {
            foreach (var @interface in handler.GetInterfaces().Where(x =>
                         x.IsGenericType && x.GetGenericTypeDefinition() ==
                         typeof(IMoDistributedEventHandler<>)))
            {
                var eventType = @interface.GetGenericArguments()[0];
                var eventName = EventNameAttribute.GetNameOrDefault(eventType);

                var subscription = new MoSubscription
                {
                    PubsubName = busOption.PubSubName,
                    Topic = eventName,
                    Route = busOption.DaprEventBusCallback,
                    Metadata = new MoMetadata
                    {
                        {
                            "rawPayload", "true"
                        }
                    }
                };
                result.Add(subscription);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets or sets the topic name.
    /// </summary>
    public string Topic { get; set; } = default!;

    /// <summary>
    /// Gets or sets the pubsub name
    /// </summary>
    public string PubsubName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the route
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Gets or sets the routes
    /// </summary>
    public MoRoutes? Routes { get; set; }

    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    public MoMetadata? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the deadletter topic.
    /// </summary>
    public string? DeadLetterTopic { get; set; }
}

/// <summary>
/// This class defines the metadata for subscribe endpoint.
/// </summary>
file class MoMetadata : Dictionary<string, string>
{
    /// <summary>
    /// Initializes a new instance of the Metadata class.
    /// </summary>
    public MoMetadata() { }

    /// <summary>
    /// Initializes a new instance of the Metadata class.
    /// </summary>
    /// <param name="dictionary"></param>
    public MoMetadata(IDictionary<string, string> dictionary) : base(dictionary) { }

    /// <summary>
    /// RawPayload key
    /// </summary>
    internal const string RawPayload = "rawPayload";
}

/// <summary>
/// This class defines the routes for subscribe endpoint.
/// </summary>
file abstract class MoRoutes
{
    /// <summary>
    /// Gets or sets the default route
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Gets or sets the routing rules
    /// </summary>
    public List<MoRule>? Rules { get; set; }
}

/// <summary>
/// This class defines the rule for subscribe endpoint.
/// </summary>
file abstract class MoRule
{
    /// <summary>
    /// Gets or sets the CEL expression to match this route.
    /// </summary>
    public string Match { get; set; } = default!;

    /// <summary>
    /// Gets or sets the path of the route.
    /// </summary>
    public string Path { get; set; } = default!;
}