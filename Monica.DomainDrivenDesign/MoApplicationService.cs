using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Features.MoMapper;
using Monica.DependencyInjection.AppInterfaces;
using Monica.DomainDrivenDesign.Interfaces;
using Monica.Core.Results;

namespace Monica.DomainDrivenDesign;


/// <summary>
/// Base class for application services, providing common properties and methods.
/// </summary>
public abstract class MoApplicationService :
    ICachedServiceProviderInjector, IMoApplicationService,
    ITransientDependency
{
    /// <summary>
    /// Gets or sets the lazy service provider.
    /// </summary>
    public ICachedServiceProvider ServiceProvider { get; set; } = null!;

    /// <summary>
    /// Gets the object mapper.
    /// </summary>
    protected IMoMapper ObjectMapper => ServiceProvider.GetRequiredService<IMoMapper>();
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