using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoMapper;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Extensions;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Repository.Registrar;
using MoLibrary.Repository.Transaction;

namespace MoLibrary.Repository;

public static class MoEfCoreServiceCollectionExtensions
{
    private static bool _isProviderRegistered;
    public static IServiceCollection AddMoUnitOfWorkDbContextProvider(this IServiceCollection services, bool addEventSupport = false)
    {
        if (_isProviderRegistered)
        {
            return services;
        }
        _isProviderRegistered = true;
        if (addEventSupport)
        {
            services.AddMoUnitOfWorkWithEvent();
        }
        else
        {
            services.AddMoUnitOfWork();
        }

        return services;
    }

    /// <summary>
    /// Adds a service of type <see cref="IDbContextProvider{TDbContext}"/> with the implementation type of <see cref="DefaultDbContextProvider{T}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>You need to manually save changes.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMoDefaultDbContextProvider(this IServiceCollection services)
    {
        if (_isProviderRegistered)
        {
            return services;
        }
        _isProviderRegistered = true;
        services.AddScoped(typeof(IDbContextProvider<>), typeof(DefaultDbContextProvider<>));
        return services;
    }


    public static IServiceCollection AddMoDbContext<TDbContext>(
        this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> optionsAction, Action<MoRepositoryOptions>? moOptionsAction = null)
        where TDbContext : MoDbContext<TDbContext>
    {
        services.AddMoMapper();

        var moOptions = new MoRepositoryOptions();

        if (moOptionsAction != null)
        {
            moOptionsAction.Invoke(moOptions);
            services.Configure(moOptionsAction);
        }

        services.AddMemoryCache();

        services.AddTransient(typeof(IDbContextProvider<>), typeof(UnitOfWorkDbContextProvider<>));


        if (moOptions.UseDbContextFactory)
        {
            services.AddDbContextFactory<TDbContext>(optionsAction);
        }

        services.AddDbContext<TDbContext>(optionsAction);

        var options = new MoEfCoreRegistrationOptions(typeof(TDbContext), services);

        services.AddTransient<IMoAuditPropertySetter, MoAuditPropertySetter>();

        services.AddTransient(serviceProvider =>
        {
            var builder = new DbContextOptionsBuilder<TDbContext>()
                .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                .UseApplicationServiceProvider(serviceProvider);
            optionsAction?.Invoke(serviceProvider, builder);
            return builder.Options;
        });

        new EfCoreRepositoryRegistrar(options).AddRepositories();

        services.AddTransient<IMoDbContextDatabaseManager<TDbContext>, MoDbContextDatabaseManager<TDbContext>>();
        return services;
    }
}

public static class ServiceCollectionRepositoryExtensions
{
    public static IServiceCollection AddMoRepository(
        this IServiceCollection services,
        Type entityType,
        Type repositoryImplementationType,
        bool replaceExisting = false)
    {
        //IBasicRepository<TEntity>
        var basicRepositoryInterface = typeof(IMoBasicRepository<>).MakeGenericType(entityType);
        if (basicRepositoryInterface.IsAssignableFrom(repositoryImplementationType))
        {
            RegisterService(services, basicRepositoryInterface, repositoryImplementationType, replaceExisting);

            //IRepository<TEntity>
            var repositoryInterface = typeof(IMoRepository<>).MakeGenericType(entityType);
            if (repositoryInterface.IsAssignableFrom(repositoryImplementationType))
            {
                RegisterService(services, repositoryInterface, repositoryImplementationType, replaceExisting);
            }
        }

        var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);
        if (primaryKeyType != null)
        {
            //IBasicRepository<TEntity, TKey>
            var basicRepositoryInterfaceWithPk = typeof(IMoBasicRepository<,>).MakeGenericType(entityType, primaryKeyType);
            if (basicRepositoryInterfaceWithPk.IsAssignableFrom(repositoryImplementationType))
            {
                RegisterService(services, basicRepositoryInterfaceWithPk, repositoryImplementationType, replaceExisting);

                //IRepository<TEntity, TKe>
                var repositoryInterfaceWithPk = typeof(IMoRepository<,>).MakeGenericType(entityType, primaryKeyType);
                if (repositoryInterfaceWithPk.IsAssignableFrom(repositoryImplementationType))
                {
                    RegisterService(services, repositoryInterfaceWithPk, repositoryImplementationType, replaceExisting);
                }
            }
        }

        return services;
    }

    private static void RegisterService(
        IServiceCollection services,
        Type serviceType,
        Type implementationType,
        bool replaceExisting)
    {
        var descriptor = ServiceDescriptor.Transient(serviceType, implementationType);

        //if (isReadOnlyRepository)
        //{
        //    services.OnActivated(descriptor, context =>
        //    {
        //        var repository = context.Instance.As<IRepository>();
        //        ObjectHelper.TrySetProperty(repository.As<IRepository>(), x => x.IsChangeTrackingEnabled, _ => false);
        //    });
        //}

        if (replaceExisting)
        {
            services.Replace(descriptor);
        }
        else
        {
            services.TryAdd(descriptor);
        }
    }
}
