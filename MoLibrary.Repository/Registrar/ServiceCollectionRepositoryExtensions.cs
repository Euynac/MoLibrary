using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Repository.Extensions;
using MoLibrary.Repository.Interfaces;

namespace MoLibrary.Repository.Registrar;


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

                //IRepository<TEntity, TKey>
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
