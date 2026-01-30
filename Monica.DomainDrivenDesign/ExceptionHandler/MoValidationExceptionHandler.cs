using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Monica.Core.ExceptionHandler;
using Monica.DomainDrivenDesign.Validation;
using Monica.Tool.MoResponse;

namespace Monica.DomainDrivenDesign.ExceptionHandler;

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

