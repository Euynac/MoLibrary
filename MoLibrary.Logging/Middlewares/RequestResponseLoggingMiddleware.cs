using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Logging.Middlewares;

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
    private const int MaxResponseSizeForLogging = 1024 * 1024; // 1MB
    private const int LogDisplayLimit = 3072;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Pre-check: Skip response logging for known file download paths
        if (ShouldSkipByPath(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;

        try
        {
            // Swap out stream with one that is buffered and supports seeking
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            // Hand over to the next middleware and wait for the call to return
            await next(context);

            // Post-check: Skip logging for file downloads or large responses
            if (IsFileDownloadResponse(context) || memoryStream.Length > MaxResponseSizeForLogging)
            {
                // Skip logging but still copy the response to the client
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);

                var logger = context.RequestServices.GetRequiredService<ILogger<ResponseLoggingMiddleware>>();
                logger.LogDebug("[Response] {StatusCode} {ContentType} - Skipped logging (file download or large response: {Size} bytes)",
                    context.Response.StatusCode, context.Response.ContentType, memoryStream.Length);
                return;
            }

            // Log response body
            var sb = new StringBuilder();
            sb.AppendLine($"[Response] {context.Response.StatusCode} {context.Response.ContentType}");
            sb.AppendLine($"[Body]");
            // Read response body from memory stream
            memoryStream.Position = 0;
            using var reader = new StreamReader(
                memoryStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 512, leaveOpen: true);

            // If stream length greater than limit, only read part of it
            if (context.Response.ContentLength > LogDisplayLimit)
            {
                var buffer = new char[LogDisplayLimit];
                await reader.ReadAsync(buffer, 0, LogDisplayLimit);
                var responseBody = new string(buffer);
                memoryStream.Position = 0;
                sb.AppendLine($"[Too large to display, only read part of it: {LogDisplayLimit}/{context.Response.ContentLength}]\n{responseBody}");
            }
            else
            {
                var responseBody = await reader.ReadToEndAsync();
                sb.AppendLine(responseBody);
            }

            // Copy body back to so its available to the user agent
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);

            var loggerInstance = context.RequestServices.GetRequiredService<ILogger<ResponseLoggingMiddleware>>();
            loggerInstance.LogDebug(sb.ToString());
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static bool ShouldSkipByPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        // Skip logging for file download endpoints
        return path.StartsWith("/logging-ui/files/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/logging-ui/current/export", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileDownloadResponse(HttpContext context)
    {
        var contentDisposition = context.Response.Headers.ContentDisposition.ToString();
        return !string.IsNullOrEmpty(contentDisposition) &&
               contentDisposition.Contains("attachment", StringComparison.OrdinalIgnoreCase);
    }
}