using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Mediator;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.Core.Results;
using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.Abstractions;

/// <summary>
/// Mark as application service
/// </summary>
public interface IApplicationService 
{

}
/// <summary>
/// Base class for application services, providing common properties and methods.
/// </summary>
public abstract class ApplicationService :
    IApplicationService,
    ITransientDependency,
    ICachedServiceProviderAccessor
{
    /// <summary>
    /// Initializes an application service with logging owned by the current host.
    /// </summary>
    /// <param name="loggerFactory">The host logger factory.</param>
    protected ApplicationService(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        Logger = loggerFactory.CreateLogger(GetType());
    }

    public ICachedServiceProvider CachedServiceProvider
    {
        get => field ?? throw CreateNotInitializedException();
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected ILogger Logger { get; }

    protected IObjectMapper Mapper => CachedServiceProvider.GetRequiredService<IObjectMapper>();

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
    /// Initializes a request handler with logging owned by the current host.
    /// </summary>
    /// <param name="loggerFactory">The host logger factory.</param>
    protected CustomApplicationService(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
    }

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
/// <param name="loggerFactory">The host logger factory.</param>
public abstract class ApplicationService<TRequest, TResponse>(ILoggerFactory loggerFactory) :
    CustomApplicationService<TRequest, Res<TResponse>>(loggerFactory)
    where TRequest : IResultRequest<TResponse>
{
}

/// <summary>
/// Base class for application services with a specific handler, request, and response wrapped in a <see cref="Res"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <param name="loggerFactory">The host logger factory.</param>
public abstract class ApplicationService<TRequest>(ILoggerFactory loggerFactory) :
    CustomApplicationService<TRequest, Res>(loggerFactory)
    where TRequest : IResultRequest
{
}
