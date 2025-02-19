using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace BuildingBlocksPlatform.AutoModel.Exceptions;

public class AutoModelExceptionHandlerForRes : IAutoModelExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case AutoModelBaseException baseException:
                httpContext.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                await httpContext.Response.WriteAsJsonAsync(new Res(baseException.Message, ResponseCode.BadRequest), cancellationToken);
                return true;
            
            default:
                return false;
        }
    }
}