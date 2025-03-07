using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocksPlatform.Repository.Interfaces;

/// <summary>
/// Represents a repository interface for managing entities of type <typeparamref name="TEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <remarks>
/// This interface extends multiple repository-related interfaces, providing a comprehensive set of methods
/// for entity management, including basic CRUD operations, explicit loading, and dependency injection support.
/// </remarks>
public interface IMoRepository<TEntity> : IMoBasicRepository<TEntity>, IMoRepository, ITransientDependency,
    ISupportsExplicitLoading<TEntity>
    where TEntity : class, IMoEntity
{
    /// <summary>
    /// Asynchronously retrieves the <see cref="DbSet{TEntity}"/> instance associated with the repository.
    /// </summary>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation, 
    /// with a result of type <see cref="DbSet{TEntity}"/>.
    /// </returns>
    /// <remarks>
    /// This method provides access to the underlying <see cref="DbSet{TEntity}"/> for the entity type <typeparamref name="TEntity"/>.
    /// It is particularly useful for scenarios requiring direct interaction with the <see cref="DbSet{TEntity}"/>,
    /// such as querying or manipulating entities using Entity Framework.
    /// </remarks>
    Task<DbSet<TEntity>> GetDbSetAsync();
}

// ReSharper disable once TypeParameterCanBeVariant
public interface IMoRepository<TEntity, TKey> : IMoRepository<TEntity>, IMoBasicRepository<TEntity, TKey>
    where TEntity : class, IMoEntity<TKey>
{

}


/// <summary>
/// 仓储层方法标记
/// </summary>
public interface IMoRepository : IMoRepositoryFeatures
{
    /// <summary>
    /// Asynchronously retrieves the <see cref="DbContext"/> instance associated with the repository.
    /// </summary>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation, 
    /// with a result of type <see cref="DbContext"/>.
    /// </returns>
    /// <remarks>
    /// This method is useful for scenarios where direct access to the underlying database context
    /// is required, such as executing raw SQL queries or leveraging advanced Entity Framework features.
    /// </remarks>
    Task<DbContext> GetDbContextAsync();
    /// <summary>
    /// Asynchronously saves all changes made in the repository to the underlying database.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation, 
    /// with a result of type <see cref="int"/> that indicates the number of state entries written to the database.
    /// </returns>
    /// <remarks>
    /// This method commits all tracked changes in the repository to the database. 
    /// It is typically used to persist changes after performing operations such as adding, updating, or deleting entities.
    /// </remarks>
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}


/// <summary>
/// 仓储层特殊功能
/// </summary>
public interface IMoRepositoryFeatures
{

    /// <summary>
    /// 该仓库是进行了分表操作
    /// </summary>
    /// <returns></returns>
    public bool IsShardingTable() => false;
}