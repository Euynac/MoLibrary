using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.ExceptionHandler;

public interface IMoExceptionHandler
{
    Task<Res> TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken);
    Task<Res> TryHandleWithCurrentHttpContextAsync(Exception exception, CancellationToken cancellationToken);
    void LogException(HttpContext? httpContext, Exception exception);
}

public interface IMoExceptionHandlerPack
{
    bool TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? res);
}