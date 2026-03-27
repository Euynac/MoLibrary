using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Monica.Logging.Services.Support;

namespace Monica.Logging.Services;

/// <summary>
/// Logs response metadata and body for HTTP responses.
/// </summary>
internal sealed class HttpResponseLoggingMiddleware(ILogger<HttpResponseLoggingMiddleware> logger) : IMiddleware
{
    private const int MaxResponseSizeForLogging = 1024 * 1024;
    private readonly ILogger<HttpResponseLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (HttpLoggingPathPolicy.ShouldSkipResponseLogging(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;

        try
        {
            await using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            await next(context);

            if (HttpLoggingPathPolicy.IsFileDownloadResponse(context) || memoryStream.Length > MaxResponseSizeForLogging)
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);

                _logger.LogDebug(
                    "[Response] {StatusCode} {ContentType} - Skipped logging (file download or large response: {Size} bytes)",
                    context.Response.StatusCode,
                    context.Response.ContentType,
                    memoryStream.Length);

                return;
            }

            var responseLog = await HttpBodyLogReader.ReadResponseBodyAsync(context.Response, memoryStream, 3072);

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);

            _logger.LogDebug("{ResponseLog}", responseLog);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
