namespace Monica.Core.Mediator;

/// <summary>
/// Dispatches a request to its matching handler.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Sends a request to its handler.
    /// </summary>
    /// <typeparam name="TResponse">The handler response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The handler response.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
