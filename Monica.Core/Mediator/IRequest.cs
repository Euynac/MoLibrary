namespace Monica.Core.Mediator;

/// <summary>
/// Represents a request that produces a response.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequest<out TResponse>
{
}
