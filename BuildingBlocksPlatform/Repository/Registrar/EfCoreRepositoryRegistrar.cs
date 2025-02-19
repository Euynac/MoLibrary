using System;
using System.Reflection;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocksPlatform.Repository.Registrar;
public class MoEfCoreRegistrationOptions(Type dbContextType, IServiceCollection services)
    : MoCommonDbContextRegistrationOptions(dbContextType, services);

public class EfCoreRepositoryRegistrar(MoEfCoreRegistrationOptions options)
    : RepositoryRegistrarBase<MoEfCoreRegistrationOptions>(options)
{
    protected override IEnumerable<Type> GetEntityTypes(Type dbContextType)
    {
        return GetEntityTypesFromDbContext(dbContextType);
    }

    protected override Type GetRepositoryType(Type dbContextType, Type entityType)
    {
        return typeof(MoRepository<,>).MakeGenericType(dbContextType, entityType);
    }

    protected override Type GetRepositoryType(Type dbContextType, Type entityType, Type primaryKeyType)
    {
        return typeof(MoRepository<,,>).MakeGenericType(dbContextType, entityType, primaryKeyType);
    }
    public static IEnumerable<Type> GetEntityTypesFromDbContext(Type dbContextType)
    {
        return
            from property in dbContextType.GetTypeInfo().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            where
                ReflectionHelper.IsAssignableToGenericType(property.PropertyType, typeof(DbSet<>)) &&
                typeof(IMoEntity).IsAssignableFrom(property.PropertyType.GenericTypeArguments[0])
            select property.PropertyType.GenericTypeArguments[0];
    }
}


