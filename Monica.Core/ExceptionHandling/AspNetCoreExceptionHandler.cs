using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.ExceptionHandling.Interfaces;
using Monica.Tool.MoResponse;

namespace Monica.Core.ExceptionHandling;

/// <summary>
/// ASP.NET Core exception handler that converts unhandled exceptions into Monica responses.
/// </summary>
public class AspNetCoreExceptionHandler(IExceptionHandlerService handler) : IExceptionHandler
{
    public virtual async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var res = await handler.HandleAsync(httpContext, exception, cancellationToken);
        handler.LogException(httpContext, exception);
        httpContext.Response.StatusCode =
            (int)(res.GetHttpStatusCode() ?? HttpStatusCode.InternalServerError);

        return await WriteResponseAsync(httpContext, res, exception, cancellationToken);
    }

    protected async ValueTask<bool> WriteResponseAsync(
        HttpContext httpContext,
        Res response,
        Exception originalException,
        CancellationToken cancellationToken)
    {
        var serializerOptions = httpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        try
        {
            var payload = JsonSerializer.Serialize(response, serializerOptions);
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            await httpContext.Response.WriteAsync(payload, cancellationToken);
            return true;
        }
        catch (Exception writeException)
        {
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<AspNetCoreExceptionHandler>>();
            logger.LogError(
                writeException,
                "Failed to serialize the exception response payload for {Path}. Original exception: {OriginalExceptionType}: {OriginalExceptionMessage}",
                httpContext.Request.Path,
                originalException.GetType().FullName,
                originalException.Message);

            if (httpContext.Response.HasStarted)
            {
                return false;
            }

            httpContext.Response.Clear();
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            httpContext.Response.ContentType = "application/json; charset=utf-8";

            var fallbackResponse = Res.Fail(response.Message ?? "服务器出现异常", response.Code ?? ResponseCode.InternalError)
                .AppendExtraInfo("detail", "Exception response serialization failed.")
                .AppendExtraInfo("originalException", new
                {
                    Type = originalException.GetType().FullName,
                    originalException.Message,
                    StackTrace = originalException.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                })
                .AppendExtraInfo("serializationException", new
                {
                    Type = writeException.GetType().FullName,
                    writeException.Message
                });

            var fallback = JsonSerializer.Serialize(fallbackResponse, serializerOptions);

            await httpContext.Response.WriteAsync(fallback, cancellationToken);
            return true;
        }
    }
}
