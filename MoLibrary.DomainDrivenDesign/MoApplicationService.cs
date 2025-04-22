using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features.MoMapper;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign;


/// <summary>
/// Base class for application services, providing common properties and methods.
/// </summary>
public abstract class MoApplicationService :
    IMoServiceProviderInjector, IMoApplicationService,
    ITransientDependency
{
    /// <summary>
    /// Gets the object mapper.
    /// </summary>
    public IMoMapper ObjectMapper => MoProvider.ServiceProvider.GetRequiredService<IMoMapper>();

    /// <summary>
    /// Gets or sets the service provider.
    /// </summary>
    public IMoServiceProvider MoProvider { get; set; }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public IServiceProvider ServiceProvider => MoProvider.ServiceProvider;
}

/// <summary>
/// Base class for custom application services with a specific handler, request, and response.
/// </summary>
/// <typeparam name="TSelfHandler">The type of the handler.</typeparam>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public abstract class MoCustomApplicationService<TSelfHandler, TRequest, TResponse> :
    MoApplicationService, IRequestHandler<TRequest, TResponse>
    where TSelfHandler : MoCustomApplicationService<TSelfHandler, TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response.</returns>
    public abstract Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for application services with a specific handler, request, and response wrapped in a <see cref="Res{T}"/>.
/// </summary>
/// <typeparam name="TSelfHandler">The type of the handler.</typeparam>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public abstract class MoApplicationService<TSelfHandler, TRequest, TResponse> :
    MoCustomApplicationService<TSelfHandler, TRequest, Res<TResponse>>
    where TSelfHandler : MoApplicationService<TSelfHandler, TRequest, TResponse> where TRequest : IMoRequest<TResponse>
{
}

/// <summary>
/// Base class for application services with a specific handler, request, and response wrapped in a <see cref="Res"/>.
/// </summary>
/// <typeparam name="TSelfHandler">The type of the handler.</typeparam>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public abstract class MoApplicationService<TSelfHandler, TRequest> :
    MoCustomApplicationService<TSelfHandler, TRequest, Res>
    where TSelfHandler : MoApplicationService<TSelfHandler, TRequest> where TRequest : IMoRequest
{
}