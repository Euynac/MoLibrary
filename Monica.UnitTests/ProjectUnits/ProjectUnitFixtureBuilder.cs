using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Authority.Identity.Abstractions;
using Monica.Core.Mediator;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.DependencyInjection.Abstractions;
using Monica.DependencyInjection.Services;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Repository.UnitOfWork.Services;
using Monica.UnitTests.Doubles;
using Monica.UnitTests.Hosting;
using Monica.UnitTests.ObjectMapping;
using NSubstitute;

namespace Monica.UnitTests.ProjectUnits;

/// <summary>
/// Configures a DI container for fast-path ProjectUnit tests.
/// </summary>
/// <typeparam name="TUnit">The ProjectUnit implementation under test.</typeparam>
public sealed class ProjectUnitFixtureBuilder<TUnit>
    where TUnit : class
{
    private readonly IServiceCollection _services = new ServiceCollection();

    /// <summary>
    /// Initializes a builder with Monica's minimum service wiring and deterministic test seams.
    /// </summary>
    public ProjectUnitFixtureBuilder()
    {
        _services.AddOptions();
        _services.AddLogging();
        _services.AddXunitTestOutputLogging();
        _services.AddScoped<ICachedServiceProvider, CachedServiceProvider>();
        _services.AddScoped<IMediator, Mediator>();
        _services.AddSingleton<ICurrentUser, TestCurrentUser>();
        _services.AddSingleton<IAsyncLocalEventPublisher, NullAsyncLocalEventPublisher>();
        _services.AddSingleton<IUnitOfWorkManager, UnitOfWorkManager>();

        var mapsterConfig = new TypeAdapterConfig();
        _services.AddSingleton(mapsterConfig);
        _services.AddScoped<IMapper, ServiceMapper>();
        _services.AddTransient<IObjectMapper>(provider => new TestObjectMapper(provider.GetRequiredService<IMapper>()));

        _services.AddMonicaTestSeams();
    }

    /// <summary>
    /// Applies additional service registrations to the fixture container.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_services);
        return this;
    }

    /// <summary>
    /// Registers a concrete service instance.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithInstance<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _services.RemoveAll<TService>();
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Registers a substitute instance for a service and returns it for inline setup.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithSubstitute<TService>(out TService substitute)
        where TService : class
    {
        substitute = Substitute.For<TService>();
        return WithInstance(substitute);
    }

    /// <summary>
    /// Registers a repository for entities without a strongly typed key.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithRepository<TEntity>(IRepository<TEntity> repository)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(repository);
        _services.RemoveAll<IRepository<TEntity>>();
        _services.AddSingleton(repository);
        return this;
    }

    /// <summary>
    /// Registers a repository for entities with a strongly typed key.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithRepository<TEntity, TKey>(IRepository<TEntity, TKey> repository)
        where TEntity : class, IEntity<TKey>
    {
        ArgumentNullException.ThrowIfNull(repository);
        _services.RemoveAll<IRepository<TEntity, TKey>>();
        _services.RemoveAll<IRepository<TEntity>>();
        _services.AddSingleton(repository);
        _services.AddSingleton<IRepository<TEntity>>(repository);
        return this;
    }

    /// <summary>
    /// Replaces the current-user service used by domain services, handlers, and repositories.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithCurrentUser(ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        _services.RemoveAll<ICurrentUser>();
        _services.AddSingleton(currentUser);
        return this;
    }

    /// <summary>
    /// Configures the Mapster rules used by the test object mapper.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithMappings(Action<TypeAdapterConfig> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return WithServices(services =>
        {
            services.RemoveAll<TypeAdapterConfig>();
            services.RemoveAll<IMapper>();
            var config = new TypeAdapterConfig();
            configure(config);
            services.AddSingleton(config);
            services.AddScoped<IMapper, ServiceMapper>();
        });
    }

    /// <summary>
    /// Registers a custom object mapper.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithMapper(IObjectMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        _services.RemoveAll<IObjectMapper>();
        _services.AddSingleton(mapper);
        return this;
    }

    /// <summary>
    /// Registers a scoped ProjectUnit and initializes its Monica cached service provider when resolved.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithProjectUnit<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _services.RemoveAll<TService>();
        _services.RemoveAll<TImplementation>();
        _services.AddScoped<TImplementation>(CreateCachedServiceProviderAwareInstance<TImplementation>);
        _services.AddScoped<TService>(provider => provider.GetRequiredService<TImplementation>());
        return this;
    }

    /// <summary>
    /// Registers a scoped ProjectUnit by its concrete type and initializes its Monica cached service provider when resolved.
    /// </summary>
    public ProjectUnitFixtureBuilder<TUnit> WithProjectUnit<TService>()
        where TService : class
    {
        _services.RemoveAll<TService>();
        _services.AddScoped(CreateCachedServiceProviderAwareInstance<TService>);
        return this;
    }

    /// <summary>
    /// Builds the fixture and resolves the ProjectUnit through the service provider.
    /// </summary>
    public ProjectUnitFixture<TUnit> Build()
    {
        var rootProvider = _services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var scope = rootProvider.CreateAsyncScope();
        var unit = CreateCachedServiceProviderAwareInstance<TUnit>(scope.ServiceProvider);
        var cachedServiceProvider = scope.ServiceProvider.GetRequiredService<ICachedServiceProvider>();

        return new ProjectUnitFixture<TUnit>(rootProvider, scope, unit, cachedServiceProvider);
    }

    private static TService CreateCachedServiceProviderAwareInstance<TService>(IServiceProvider provider)
        where TService : class
    {
        var instance = ActivatorUtilities.CreateInstance<TService>(provider);

        if (instance is ICachedServiceProviderAccessor accessor)
        {
            accessor.CachedServiceProvider = provider.GetRequiredService<ICachedServiceProvider>();
        }

        return instance;
    }
}
