using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Monica.Logging.Services.Support;

namespace Monica.Logging.Services;

/// <summary>
/// Logs request metadata, body, and headers for HTTP requests.
/// </summary>
internal sealed class HttpRequestLoggingMiddleware(ILogger<HttpRequestLoggingMiddleware> logger) : IMiddleware
{
    private readonly ILogger<HttpRequestLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.EnableBuffering();

        var request = context.Request;
        var logBuilder = new List<string>
        {
            $"[Request] {request.Method} {request.Path} {request.QueryString} ({request.Protocol})"
        };

        if (request.Body.CanRead && (request.Method == HttpMethods.Post || request.Method == HttpMethods.Put))
        {
            var bodyLog = await HttpBodyLogReader.ReadRequestBodyAsync(request, 3072);
            if (!string.IsNullOrEmpty(bodyLog))
            {
                logBuilder.Add("[Body]");
                logBuilder.Add(bodyLog);
            }
        }

        if (request.Headers.Count > 0)
        {
            logBuilder.Add("[RequestHeaders]");
            foreach (var header in request.Headers)
            {
                logBuilder.Add($"{header.Key}: {header.Value}");
            }
        }

        _logger.LogDebug("{RequestLog}", string.Join(Environment.NewLine, logBuilder));

        await next(context);
    }
}
