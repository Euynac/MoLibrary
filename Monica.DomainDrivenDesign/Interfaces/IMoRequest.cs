using Monica.Core.Mediator;
using Monica.Core.Results;

namespace Monica.DomainDrivenDesign.Interfaces;
/// <summary>
/// Base interface for request contracts; this type should not be used directly.
/// </summary>
public interface IMoRequestBase
{

}

/// <summary>
/// Request interface that returns a typed result.
/// </summary>
/// <typeparam name="TRequest">The response type for the request.</typeparam>
public interface IMoRequest<TRequest> : IRequest<Res<TRequest>>, IMoRequestBase
{
    
}

/// <summary>
/// Simple request interface.
/// </summary>
public interface IMoRequest : IRequest<Res>, IMoRequestBase
{

}

/// <summary>
/// Custom request interface.
/// </summary>
/// <typeparam name="TRequest">The result type returned by the custom request.</typeparam>
public interface IMoCustomRequest<out TRequest> : IRequest<TRequest>
{

}
