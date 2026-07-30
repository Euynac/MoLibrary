namespace Monica.Core.Mediator;

/// <summary>
/// Handles a specific request type.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the incoming request.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The handler response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
