using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.ExceptionHandling.Exceptions;
using Monica.Core.ExceptionHandling.Models.Internal;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Monica.Modules;

namespace Monica.Core.ExceptionHandling.Services;

internal class ExceptionHandlerService(
    ILogger<ExceptionHandlerService> logger,
    IHttpContextAccessor accessor,
    IEnumerable<IExceptionResponseMapper> mappers,
    IOptions<ModuleExceptionHandlingOption> options) : IExceptionHandlerService
{
    private const string UNEXPECTED_ERROR_MESSAGE = "An unexpected server error occurred.";
    private readonly ModuleExceptionHandlingOption _options = options.Value;

    public Task<Res> HandleCurrentHttpContextAsync(Exception exception, CancellationToken cancellationToken)
    {
        return HandleAsync(accessor.HttpContext, exception, cancellationToken);
    }

    public void LogException(HttpContext? httpContext, Exception exception)
    {
        logger.LogError(exception, $"{httpContext?.Request.Path} threw an exception");
    }

    public Task<Res> HandleAsync(HttpContext? httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (actualException, metadataEntries) = UnwrapException(exception);

        foreach (var mapper in mappers)
        {
            if (mapper.TryMap(httpContext, actualException, cancellationToken, out var response))
            {
                return Task.FromResult(AppendMetadataEntries(response!));
            }
        }

        var includeContextualMetadata = true;
        Res result = actualException switch
        {
            BusinessException businessException => Res.Fail(businessException.Message),
            DisplayMessageException displayMessageException => CreateDisplayMessageResponse(displayMessageException),
            _ => CreateUnexpectedErrorResponse(httpContext, actualException)
        };

        if (result.Status == ResStatus.InternalError && !_options.IncludeExceptionDetails)
        {
            includeContextualMetadata = false;
        }

        return Task.FromResult(includeContextualMetadata ? AppendMetadataEntries(result) : result);

        T AppendMetadataEntries<T>(T response) where T : IResultEnvelope
        {
            foreach (var kvp in metadataEntries)
            {
                response.AppendMetadata(kvp.Key, kvp.Value);
            }
            return response;
        }
    }

    /// <summary>
    /// Unwraps nested <see cref="ContextualException"/> instances and collects their extra metadata.
    /// </summary>
    /// <param name="exception">The original exception.</param>
    /// <returns>The actual exception together with all collected metadata entries.</returns>
    private static (Exception ActualException, List<KeyValuePair<string, object?>> Metadata) UnwrapException(Exception exception)
    {
        var metadataEntries = new List<KeyValuePair<string, object?>>();
        var currentException = exception;

        while (currentException is ContextualException contextualException)
        {
            metadataEntries.AddRange(contextualException.Metadata);

            if (contextualException.InnerException == null)
            {
                break;
            }

            currentException = contextualException.InnerException;
        }

        return (currentException, metadataEntries);
    }

    private static Res CreateDisplayMessageResponse(DisplayMessageException exception)
    {
        var response = new Res(exception.DisplayMessage, exception.ResultStatus);
        if (exception.TechnicalDetail != null)
        {
            response.AppendMetadata("detail", exception.TechnicalDetail);
        }

        return response;
    }

    private Res CreateUnexpectedErrorResponse(HttpContext? httpContext, Exception exception)
    {
        if (!_options.IncludeExceptionDetails)
        {
            return Res.Fail(UNEXPECTED_ERROR_MESSAGE, ResStatus.InternalError);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = exception.GetMessageRecursively(),
            Extensions =
            {
                ["stackTrace"] = exception.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries),
                ["response"] = HttpResponseSnapshot.Create(httpContext?.Response),
                ["request"] = HttpRequestSnapshot.Create(httpContext?.Request),
                ["connection"] = ConnectionSnapshot.Create(httpContext?.Connection),
                ["path"] = httpContext?.Request.GetDisplayUrl(),
                ["endpoint"] = httpContext?.GetEndpoint()?.DisplayName,
                ["time"] = DateTime.Now,
                ["utcTime"] = DateTime.UtcNow,
            }
        };

        return Res.Fail(UNEXPECTED_ERROR_MESSAGE, ResStatus.InternalError)
            .AppendMetadata("error", problemDetails);
    }
}
