using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoMapper;
using Monica.Core.Logging;
using Monica.Core.Mediator;
using Monica.Core.Results;
using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.Abstractions;


/// <summary>
/// Base class for application services, providing common properties and methods.
/// </summary>
public abstract class ApplicationService :
    IApplicationService,
    ITransientDependency,
    ICachedServiceProviderAccessor
{
    private readonly Lazy<ILogger> _loggerLazy;

    protected ApplicationService()
    {
        _loggerLazy = new Lazy<ILogger>(() => LogManager.For(GetType()));
    }

    public ICachedServiceProvider CachedServiceProvider
    {
        get => field ?? throw CreateNotInitializedException();
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected ILogger Logger => _loggerLazy.Value;

    protected IMoMapper Mapper => CachedServiceProvider.GetRequiredService<IMoMapper>();

    private InvalidOperationException CreateNotInitializedException()
    {
        return new InvalidOperationException(
            $"Cached service provider is not initialized for {GetType().FullName}. Resolve the service through Monica DI instead of constructing it manually.");
    }
}

/// <summary>
/// Base class for custom application services with a specific handler, request, and response.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public abstract class CustomApplicationService<TRequest, TResponse> :
    ApplicationService, IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
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
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public abstract class ApplicationService<TRequest, TResponse> :
    CustomApplicationService<TRequest, Res<TResponse>>
    where TRequest : IMoRequest<TResponse>
{
}

/// <summary>
/// Base class for application services with a specific handler, request, and response wrapped in a <see cref="Res"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
public abstract class ApplicationService<TRequest> :
    CustomApplicationService<TRequest, Res>
    where TRequest : IMoRequest
{
}
