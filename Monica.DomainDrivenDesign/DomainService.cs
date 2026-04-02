using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Features.MoMapper;
using Monica.Core.Logging;
using Monica.DependencyInjection.Abstractions;
using Monica.DomainDrivenDesign.Interfaces;

namespace Monica.DomainDrivenDesign;

public abstract class DomainService : IDomainService, ICachedServiceProviderAccessor
{
    private readonly Lazy<ILogger> _loggerLazy;

    protected DomainService()
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
