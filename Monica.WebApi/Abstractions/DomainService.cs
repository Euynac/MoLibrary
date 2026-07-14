using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.Abstractions;
/// <summary>
/// This interface can be implemented by all domain services to identify them by convention.
/// </summary>
public interface IDomainService : ITransientDependency
{

}

public abstract class DomainService : IDomainService, ICachedServiceProviderAccessor
{
    /// <summary>
    /// Initializes a domain service with logging owned by the current host.
    /// </summary>
    /// <param name="loggerFactory">The host logger factory.</param>
    protected DomainService(ILoggerFactory loggerFactory)
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
