using Microsoft.AspNetCore.Http;
using Monica.Tool.Results;

namespace Monica.Core.ExceptionHandling.Abstractions;

public interface IExceptionHandlerService
{
    Task<Res> HandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken);
    Task<Res> HandleCurrentHttpContextAsync(Exception exception, CancellationToken cancellationToken);
    void LogException(HttpContext? httpContext, Exception exception);
}
