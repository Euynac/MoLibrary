using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Extensions;

namespace Monica.Repository.Persistence.Services.Support;


public static class ServiceCollectionRepositoryExtensions
{
    public static IServiceCollection AddRepositoryServices(
        this IServiceCollection services,
        Type entityType,
        Type repositoryImplementationType,
        bool replaceExisting = false)
    {
        //IBasicRepository<TEntity>
        var basicRepositoryInterface = typeof(IBasicRepository<>).MakeGenericType(entityType);
        if (basicRepositoryInterface.IsAssignableFrom(repositoryImplementationType))
        {
            RegisterService(services, basicRepositoryInterface, repositoryImplementationType, replaceExisting);

            //IRepository<TEntity>
            var repositoryInterface = typeof(IRepository<>).MakeGenericType(entityType);
            if (repositoryInterface.IsAssignableFrom(repositoryImplementationType))
            {
                RegisterService(services, repositoryInterface, repositoryImplementationType, replaceExisting);
            }
        }

        var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);
        if (primaryKeyType != null)
        {
            //IBasicRepository<TEntity, TKey>
            var basicRepositoryInterfaceWithPk = typeof(IBasicRepository<,>).MakeGenericType(entityType, primaryKeyType);
            if (basicRepositoryInterfaceWithPk.IsAssignableFrom(repositoryImplementationType))
            {
                RegisterService(services, basicRepositoryInterfaceWithPk, repositoryImplementationType, replaceExisting);

                //IRepository<TEntity, TKey>
                var repositoryInterfaceWithPk = typeof(IRepository<,>).MakeGenericType(entityType, primaryKeyType);
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
