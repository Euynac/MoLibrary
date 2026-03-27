using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.ExceptionHandling.Exceptions;
using Monica.Core.ExceptionHandling.Models.Internal;
using Monica.Core.Extensions;
using Monica.Tool.Results;

namespace Monica.Core.ExceptionHandling.Services;

internal class ExceptionHandlerService(
    ILogger<ExceptionHandlerService> logger,
    IHttpContextAccessor accessor,
    IEnumerable<IExceptionResponseMapper> mappers) : IExceptionHandlerService
{
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
        var (actualException, extraInfoList) = UnwrapException(exception);

        foreach (var mapper in mappers)
        {
            if (mapper.TryMap(httpContext, actualException, cancellationToken, out var response))
            {
                return Task.FromResult(AppendExtraInfoList(response!));
            }
        }

        Res result = actualException switch
        {
            BusinessException businessException => Res.Fail(businessException.Message),
            DisplayMessageException displayMessageException => CreateDisplayMessageResponse(displayMessageException),
            _ => CreateUnexpectedErrorResponse(httpContext, actualException)
        };

        return Task.FromResult(AppendExtraInfoList(result));

        T AppendExtraInfoList<T>(T response) where T : IResultEnvelope
        {
            foreach (var kvp in extraInfoList)
            {
                response.AppendExtraInfo(kvp.Key, kvp.Value);
            }
            return response;
        }
    }

    /// <summary>
    /// Unwraps nested <see cref="ContextualException"/> instances and collects their extra metadata.
    /// </summary>
    /// <param name="exception">The original exception.</param>
    /// <returns>The actual exception together with all collected extra info entries.</returns>
    private static (Exception ActualException, List<KeyValuePair<string, object?>> ExtraInfo) UnwrapException(Exception exception)
    {
        var extraInfoList = new List<KeyValuePair<string, object?>>();
        var currentException = exception;

        while (currentException is ContextualException contextualException)
        {
            extraInfoList.AddRange(contextualException.ExtraInfo);

            if (contextualException.InnerException == null)
            {
                break;
            }

            currentException = contextualException.InnerException;
        }

        return (currentException, extraInfoList);
    }

    private static Res CreateDisplayMessageResponse(DisplayMessageException exception)
    {
        var response = new Res(exception.DisplayMessage, exception.ResponseCode);
        if (exception.TechnicalDetail != null)
        {
            response.AppendExtraInfo("detail", exception.TechnicalDetail);
        }

        return response;
    }

    private static Res CreateUnexpectedErrorResponse(HttpContext? httpContext, Exception exception)
    {
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

        return Res.Fail("服务器出现异常", ResStatus.InternalError)
            .AppendExtraInfo("error", problemDetails);
    }
}
