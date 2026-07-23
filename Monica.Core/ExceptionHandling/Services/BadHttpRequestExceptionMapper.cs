using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.Results;

namespace Monica.Core.ExceptionHandling.Services;

/// <summary>
/// Converts ASP.NET Core request-binding failures into the standard Monica response envelope.
/// </summary>
internal sealed class BadHttpRequestExceptionMapper : IExceptionResponseMapper
{
    public bool TryMap(
        HttpContext? httpContext,
        Exception exception,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? response)
    {
        if (exception is not BadHttpRequestException badRequestException)
        {
            response = null;
            return false;
        }

        var detail = badRequestException.InnerException is JsonException jsonException
            ? jsonException.Message
            : badRequestException.Message;
        response = Res.Fail($"The request is invalid: {detail}", MapStatus(badRequestException.StatusCode));
        return true;
    }

    private static ResStatus MapStatus(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status413PayloadTooLarge => ResStatus.PayloadTooLarge,
            StatusCodes.Status415UnsupportedMediaType => ResStatus.UnsupportedMediaType,
            _ => ResStatus.BadRequest
        };
    }
}
