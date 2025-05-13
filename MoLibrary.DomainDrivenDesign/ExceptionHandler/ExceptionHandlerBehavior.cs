using MediatR;
using Microsoft.AspNetCore.Http;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.ExceptionHandler;

/// <summary>
/// Middleware for handling exceptions in MediatR pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class ExceptionHandlerBehavior<TRequest, TResponse>(IMoExceptionHandler handler, IHttpContextAccessor accessor)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse> where TResponse : IServiceResponse, new()
{
    /// <summary>
    /// Handles the request and catches any exceptions that occur during processing.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next.Invoke();
        }
        catch (Exception e)
        {
            handler.LogException(accessor.HttpContext, e);
            return (await handler.TryHandleAsync(accessor.HttpContext, e, cancellationToken))
                .ToServiceResponse<TResponse>();
        }
    }
}