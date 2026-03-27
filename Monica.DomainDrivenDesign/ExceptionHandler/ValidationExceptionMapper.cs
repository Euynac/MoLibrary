using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.DomainDrivenDesign.Validation;
using Monica.Tool.Results;

namespace Monica.DomainDrivenDesign.ExceptionHandler;

internal class ValidationExceptionMapper : IExceptionResponseMapper
{
    public bool TryMap(
        HttpContext? httpContext,
        Exception exception,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? response)
    {
        switch (exception)
        {
            case MoValidationException validationException:
                response = Res.Fail("接口请求参数校验失败", ResStatus.ValidateError)
                    .AppendMetadata("error", validationException.ValidationErrors);
                return true;
            default:
                response = null;
                return false;
        }
    }
}
