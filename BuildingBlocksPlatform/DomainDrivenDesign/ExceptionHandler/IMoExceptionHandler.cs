using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Http;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.DomainDrivenDesign.ExceptionHandler;

public interface IMoExceptionHandler
{
    public Task<Res> TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken);
    public void LogException(HttpContext? httpContext, Exception exception);
}