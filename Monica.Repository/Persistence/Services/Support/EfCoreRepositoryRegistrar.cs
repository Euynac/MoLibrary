using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monica.Repository.Entity.Abstractions;
using Monica.Tool.Helpers;

namespace Monica.Repository.Persistence.Services.Support;
public class EfRepositoryRegistrationOptions(Type dbContextType, IServiceCollection services)
    : DbContextRegistrationOptionsBase(dbContextType, services);

public class EfCoreRepositoryRegistrar(EfRepositoryRegistrationOptions options)
    : RepositoryRegistrarBase<EfRepositoryRegistrationOptions>(options)
{
    protected override IEnumerable<Type> GetEntityTypes(Type dbContextType)
    {
        return GetEntityTypesFromDbContext(dbContextType);
    }

    protected override Type GetRepositoryType(Type dbContextType, Type entityType)
    {
        return typeof(EfRepository<,>).MakeGenericType(dbContextType, entityType);
    }

    protected override Type GetRepositoryType(Type dbContextType, Type entityType, Type primaryKeyType)
    {
        return typeof(EfRepository<,,>).MakeGenericType(dbContextType, entityType, primaryKeyType);
    }
    public static IEnumerable<Type> GetEntityTypesFromDbContext(Type dbContextType)
    {
        return
            from property in dbContextType.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            where
                ReflectionHelper.IsAssignableToGenericType(property.PropertyType, typeof(DbSet<>)) &&
                typeof(IEntity).IsAssignableFrom(property.PropertyType.GenericTypeArguments[0])
            select property.PropertyType.GenericTypeArguments[0];
    }
}


