using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.ExceptionHandler;

/// <summary>
/// Global exception handler for handling exceptions in the application.
/// </summary>
public class DefaultGlobalExceptionHandler(IMoExceptionHandler handler) : IExceptionHandler
{
    public virtual async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var res = await handler.TryHandleAsync(httpContext, exception, cancellationToken);
        handler.LogException(httpContext, exception);
        httpContext.Response.StatusCode =
            (int)(((IServiceResponse?)res)?.GetHttpStatusCode() ?? HttpStatusCode.InternalServerError);

        await httpContext.Response.WriteAsJsonAsync(res, cancellationToken);
        return true;
    }
}