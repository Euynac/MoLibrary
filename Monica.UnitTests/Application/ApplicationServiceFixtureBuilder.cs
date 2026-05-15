using Microsoft.Extensions.DependencyInjection;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.WebApi.Abstractions;
using NSubstitute;

namespace Monica.UnitTests.Application;

/// <summary>
/// Configures services for an <see cref="ApplicationServiceFixture{THandler}"/>.
/// </summary>
/// <typeparam name="THandler">The application service under test.</typeparam>
public sealed class ApplicationServiceFixtureBuilder<THandler>
    where THandler : ApplicationService
{
    private readonly IServiceCollection _services = new ServiceCollection();

    /// <summary>
    /// Initializes a new builder with Monica's minimum service wiring.
    /// </summary>
    public ApplicationServiceFixtureBuilder()
    {
        _services.AddOptions();
        _services.AddLogging();
        _services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
    }

    /// <summary>
    /// Applies additional service registrations to the fixture container.
    /// </summary>
    public ApplicationServiceFixtureBuilder<THandler> WithServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_services);
        return this;
    }

    /// <summary>
    /// Registers a concrete service instance.
    /// </summary>
    public ApplicationServiceFixtureBuilder<THandler> WithInstance<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Registers a substitute instance for a service and returns it for inline setup.
    /// </summary>
    public ApplicationServiceFixtureBuilder<THandler> WithSubstitute<TService>(out TService substitute)
        where TService : class
    {
        substitute = Substitute.For<TService>();
        _services.AddSingleton(substitute);
        return this;
    }

    /// <summary>
    /// Registers a custom object mapper for handlers that use mapping logic.
    /// </summary>
    public ApplicationServiceFixtureBuilder<THandler> WithMapper(IObjectMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _services.AddSingleton(mapper);
        return this;
    }

    /// <summary>
    /// Builds the fixture and resolves the handler through the Monica DI container.
    /// </summary>
    public ApplicationServiceFixture<THandler> Build()
    {
        var rootProvider = _services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var scope = rootProvider.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<THandler>(scope.ServiceProvider);
        var cachedServiceProvider = scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>();

        if (handler is ICachedServiceProviderAccessor accessor)
        {
            accessor.CachedServiceProvider = cachedServiceProvider;
        }
        else
        {
            throw new InvalidOperationException(
                $"{typeof(THandler).FullName} must implement {nameof(ICachedServiceProviderAccessor)}.");
        }

        return new ApplicationServiceFixture<THandler>(rootProvider, scope, handler, cachedServiceProvider);
    }
}
