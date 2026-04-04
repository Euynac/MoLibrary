using Monica.Repository.Persistence.Extensions;

namespace Monica.Repository.Persistence.Services.Support;

public abstract class RepositoryRegistrarBase<TOptions>(TOptions options)
    where TOptions : DbContextRegistrationOptionsBase
{
    public TOptions Options { get; } = options;

    public virtual void AddRepositories()
    {
        RegisterDefaultRepositories();
    }

    protected virtual void RegisterDefaultRepositories()
    {
        foreach (var entityType in GetEntityTypes(Options.DbContextType))
        {
            RegisterDefaultRepository(entityType);
        }
    }


    protected virtual void RegisterDefaultRepository(Type entityType)
    {
        Options.Services.AddRepositoryServices(
            entityType,
            GetDefaultRepositoryImplementationType(entityType)
        );
    }

    protected virtual Type GetDefaultRepositoryImplementationType(Type entityType)
    {
        var primaryKeyType = EntityHelper.FindPrimaryKeyType(entityType);

        return primaryKeyType == null ? GetRepositoryType(Options.DbContextType, entityType) : GetRepositoryType(Options.DbContextType, entityType, primaryKeyType);
    }


    protected abstract IEnumerable<Type> GetEntityTypes(Type dbContextType);

    protected abstract Type GetRepositoryType(Type dbContextType, Type entityType);

    protected abstract Type GetRepositoryType(Type dbContextType, Type entityType, Type primaryKeyType);
}
