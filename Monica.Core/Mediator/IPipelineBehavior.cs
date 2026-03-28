namespace Monica.Core.Mediator;

/// <summary>
/// Represents the next step in the request pipeline.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <returns>The pipeline response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Intercepts request handling before or after the main handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request and optionally delegates to the next pipeline step.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="next">The next pipeline step.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pipeline response.</returns>
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
