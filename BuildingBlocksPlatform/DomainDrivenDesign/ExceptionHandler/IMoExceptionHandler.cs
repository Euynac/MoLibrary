using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocksPlatform.DomainDrivenDesign.ExceptionHandler;

public interface IMoExceptionHandler
{
    public Task<Res> TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken);
    public void LogException(HttpContext? httpContext, Exception exception);
}