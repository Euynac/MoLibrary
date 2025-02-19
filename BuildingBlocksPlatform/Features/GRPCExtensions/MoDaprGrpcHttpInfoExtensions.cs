using BuildingBlocksPlatform.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using BuildingBlocksPlatform.Converters;
using BuildingBlocksPlatform.SeedWork;
using Dapr.Actors.Runtime;
using Koubot.Tool.Extensions;

namespace BuildingBlocksPlatform.Features.GRPCExtensions;

public static class MoDaprGrpcHttpInfoExtensions
{
    public static void AddMoDaprGrpcApiHttpInfo(this IServiceCollection services)
    {
        services.AddTransient<MoGrpcApiHttpInfoMiddleware>();
    }
    /// <summary>
    ///  注册请求响应日志中间件
    ///  </summary>
    ///  <param name="builder"></param>
    /// <returns></returns>
    public static void UseMoDaprGrpcApiHttpInfo(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<MoGrpcApiHttpInfoMiddleware>();
    }
}


/// <summary>
/// Middleware for logging request body and query string
/// </summary>
internal sealed class MoGrpcApiHttpInfoMiddleware(IGlobalJsonOption jsonOption) : IMiddleware
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

        // Call next middleware in the pipeline
        await next(context);
    }
}