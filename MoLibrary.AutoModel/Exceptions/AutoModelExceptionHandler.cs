using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using MoLibrary.Core.ExceptionHandler;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.AutoModel.Exceptions;

public class AutoModelExceptionHandler : IMoExceptionHandlerPack
{
    public bool TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken, [NotNullWhen(true)] out Res? res)
    {
        switch (exception)
        {
            case AutoModelBaseException baseException:
                res = new Res(baseException.Message, ResponseCode.BadRequest);
                return true;

            default:
                res = null;
                return false;
        }
    }
}