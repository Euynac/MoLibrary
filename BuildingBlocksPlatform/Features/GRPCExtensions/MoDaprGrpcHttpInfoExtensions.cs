using System.Dynamic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json.Nodes;
using BuildingBlocksPlatform.Converters;
using BuildingBlocksPlatform.DomainDrivenDesign.ExceptionHandler;
using BuildingBlocksPlatform.SeedWork;
using BuildingBlocksPlatform.Features.Decorators;
using System.Net.Http;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Features;
using MoLibrary.Core.UtilsAbstract;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.Features.GRPCExtensions;

public static class MoDaprGrpcHttpInfoExtensions
{
    public static void AddMoDaprGrpcApiHttpInfo(this IServiceCollection services)
    {
        services.AddTransient<MoGrpcApiHttpInfoMiddleware>();
    }

    public static void UseMoDaprGrpcApiHttpInfo(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<MoGrpcApiHttpInfoMiddleware>();
    }
}


/// <summary>
/// 用于扩展Dapr调用相关功能，如自定义Header、链路追踪
/// </summary>
internal sealed class MoGrpcApiHttpInfoMiddleware(IGlobalJsonOption jsonOption, IMoExceptionHandler handler) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint()?.DisplayName != "Dapr Actors Invoke")
        {
            await next(context);
            return;
        }

        // Ensure the request body can be read multiple times
        context.Request.EnableBuffering();

        if (context.Request.Body.CanRead)
        {
            //// Leave stream open so next middleware can read it
            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 512, leaveOpen: true);
            var requestBody = await reader.ReadToEndAsync();
            //// Reset stream position, so next middleware can read it

            context.Request.Body.Position = 0;
            try
            {
                if (JsonSerializer.Deserialize<OurRequest>(context.Request.Body, jsonOption.GlobalOptions) is {Headers.Count: > 0} request)
                {
                    foreach (var (key, value) in request.Headers.Where(p=>p.Key.StartsWith("X-")))
                    {

                        if (!context.Request.Headers.ContainsKey(key))
                        {
                            context.Request.Headers.Add(key, value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                
            }


            context.Request.Body.Position = 0;
        }


        var originalBodyStream = context.Response.Body;

        // Swap out stream with one that is buffered and supports seeking
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;
        try
        {
            // Hand over to the next middleware and wait for the call to return
            await next(context);

            memoryStream.Position = 0;
            var originResponse =
                await JsonSerializer.DeserializeAsync<JsonNode>(memoryStream, jsonOption.GlobalOptions);

            using var newResStream = new MemoryStream();
            context.Response.Body = newResStream;

            originResponse![nameof(Res.ExtraInfo)] =
                JsonSerializer.SerializeToNode(context.GetOrDefault<MoRequestContext>(), jsonOption.GlobalOptions);

            await context.Response.WriteAsJsonAsync(originResponse, jsonOption.GlobalOptions);

            // Copy body back to so its available to the user agent
            newResStream.Position = 0;
            await newResStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception e)
        {
            var exRes = await handler.TryHandleAsync(context, e, CancellationToken.None);
            handler.LogException(context, e);
            using var exceptionStream = new MemoryStream();
            context.Response.Body = exceptionStream;
            await context.Response.WriteAsJsonAsync(exRes);
            exceptionStream.Position = 0;
            await exceptionStream.CopyToAsync(originalBodyStream);
            //巨坑：必须使用原有HTTPContext的Stream，猜想：因为独自开的Stream被using自动Dispose掉了，返回后无法读取。

        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}