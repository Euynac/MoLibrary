using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Logging.Middlewares;


public static class MiddlewareExtensions
{
    public static void AddRequestResponseLogging(this IServiceCollection services)
    {
        services.AddTransient<RequestLoggingMiddleware>();
        services.AddTransient<ResponseLoggingMiddleware>();
    }
    /// <summary>
    ///  注册请求响应日志中间件
    ///  </summary>
    ///  <param name="builder"></param>
    /// <param name="disableResponse"></param>
    /// <param name="disableRequest"></param>
    /// <returns></returns>
    public static void UseRequestResponseLogging(this IApplicationBuilder builder, bool disableResponse = false, bool disableRequest = false)
    {
        if (!disableRequest)
        {
            builder.UseMiddleware<RequestLoggingMiddleware>();
        }

        if (!disableResponse)
        {
            builder.UseMiddleware<ResponseLoggingMiddleware>();
        }

        //builder.UseHttpLogging(); //asp.net 8后启用
    }
}

/// <summary>
/// Middleware for logging request body and query string
/// </summary>
internal sealed class RequestLoggingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;

        // Ensure the request body can be read multiple times
        context.Request.EnableBuffering();

        var sb = new StringBuilder();
        sb.AppendLine(
            $"[Request] {method} {context.Request.Path} {context.Request.QueryString} ({context.Request.Protocol})");
        // Only if we are dealing with POST or PUT, GET and others shouldn't have a body
        if (context.Request.Body.CanRead && (method == HttpMethods.Post || method == HttpMethods.Put))
        {
            // Leave stream open so next middleware can read it
            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 512, leaveOpen: true);
            sb.AppendLine("[Body]");
            var limit = 3072;
            //if stream length greater than limit, only read part of it
            if (context.Request.ContentLength == null || context.Request.ContentLength > limit)
            {
                var buffer = new char[limit];
                await reader.ReadAsync(buffer, 0, limit);
                var requestBody = new string(buffer);
                context.Request.Body.Position = 0;
                sb.AppendLine($"[Too large to display, only read part of it: {limit}/{context.Request.ContentLength?.ToString() ?? "unknown"}]\n{requestBody}");
                
            }
            else
            {
                var requestBody = await reader.ReadToEndAsync();
                sb.AppendLine(requestBody);
            }
            // Reset stream position, so next middleware can read it
            context.Request.Body.Position = 0;
        }


        // Additionally log headers
        if (!context.Request.Headers.IsNullOrEmptySet())
        {
            sb.AppendLine($"[RequestHeaders]\n");
            foreach (var header in context.Request.Headers)
                sb.AppendLine($"{header.Key}: {header.Value}");
        }
        var logger = context.RequestServices.GetRequiredService<ILogger<ResponseLoggingMiddleware>>();
        logger.LogDebug(sb.ToString());
        // Call next middleware in the pipeline
        await next(context);
    }
}

/// <summary>
/// Middleware for logging response body
/// </summary>
internal sealed class ResponseLoggingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var originalBodyStream = context.Response.Body;

        try
        {
            // Swap out stream with one that is buffered and supports seeking
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            // Hand over to the next middleware and wait for the call to return
            await next(context);


            // Log response body
            var sb = new StringBuilder();
            sb.AppendLine($"[Response] {context.Response.StatusCode} {context.Response.ContentType}");
            sb.AppendLine($"[Body]");
            // Read response body from memory stream
            memoryStream.Position = 0;
            //var reader = new StreamReader(memoryStream);
            using var reader = new StreamReader(
                memoryStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 512, leaveOpen: true);
            var limit = 3072;
            //if stream length greater than limit, only read part of it
            if (context.Response.ContentLength > limit)
            {
                var buffer = new char[limit];
                await reader.ReadAsync(buffer, 0, limit);
                var responseBody = new string(buffer);
                context.Request.Body.Position = 0;
                sb.AppendLine($"[Too large to display, only read part of it: {limit}/{context.Request.ContentLength}]\n{responseBody}");
                
            }
            else
            {
                var requestBody = await reader.ReadToEndAsync();
                sb.AppendLine(requestBody);
            }

           

            // Copy body back to so its available to the user agent
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);

         
            var logger = context.RequestServices.GetRequiredService<ILogger<ResponseLoggingMiddleware>>();
            logger.LogDebug(sb.ToString());
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}