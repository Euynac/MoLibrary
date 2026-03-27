using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Monica.Tool.MoResponse;

namespace Monica.Core.ExceptionHandling.Abstractions;

public interface IExceptionResponseMapper
{
    bool TryMap(
        HttpContext? httpContext,
        Exception exception,
        CancellationToken cancellationToken,
        [NotNullWhen(true)] out Res? response);
}
