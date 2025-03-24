using Microsoft.AspNetCore.Http;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.ExceptionHandler;

public interface IMoExceptionHandler
{
    public Task<Res> TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken);
    public void LogException(HttpContext? httpContext, Exception exception);
}