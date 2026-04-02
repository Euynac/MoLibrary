using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.Results;

namespace Monica.WebApi.Validation;

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
            case RequestValidationException validationException:
                response = Res.Fail("Request parameter validation failed.", ResStatus.ValidateError)
                    .AppendMetadata("error", validationException.ValidationErrors);
                return true;
            default:
                response = null;
                return false;
        }
    }
}
