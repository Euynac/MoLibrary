using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using MoLibrary.Core.ExceptionHandler;
using MoLibrary.DomainDrivenDesign.Validation;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.ExceptionHandler;

internal class MoValidationExceptionHandler : IMoExceptionHandlerPack
{
    public bool TryHandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? res)
    {
        switch (exception)
        {
            case MoValidationException validationException:
                res = Res.CreateError<IList<ValidationResult>>(validationException.ValidationErrors, "接口请求参数校验失败",
                    ResponseCode.ValidateError);
                return true;
            default:
                res = null;
                return false;
        }
    }
}

