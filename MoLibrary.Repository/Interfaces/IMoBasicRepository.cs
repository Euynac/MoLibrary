using System.Linq.Expressions;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Exceptions;

namespace MoLibrary.Repository.Interfaces;

public interface IMoBasicRepository<TEntity>
    where TEntity : class, IMoEntity
{
    Task<IQueryable<TEntity>> WithDetailsAsync();

    Task<IQueryable<TEntity>> WithDetailsAsync(
        params Expression<Func<TEntity, object>>[] propertySelectors);

    Task<IQueryable<TEntity>> GetQueryableAsync();

    /// <summary>
    /// Gets a list of entities by the given <paramref name="predicate" />.
    /// </summary>
    /// <param name="predicate">A condition to filter the entities</param>
    /// <param name="includeDetails">Set true to include details (sub-collections) of this entity</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    Task<List<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a single entity by the given <paramref name="predicate"/>.
    /// <para>
    /// It returns null if there is no entity with the given <paramref name="predicate"/>.
    /// It throws <see cref="InvalidOperationException"/> if there are multiple entities with the given <paramref name="predicate"/>.
    /// </para>
    /// </summary>
    /// <param name="predicate">A condition to find the entity</param>
    /// <param name="includeDetails">Set true to include all children of this entity</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a single entity by the given <paramref name="predicate"/>.
    /// <para>
    /// It throws <see cref="EntityNotFoundException"/> if there is no entity with the given <paramref name="predicate"/>.
    /// It throws <see cref="InvalidOperationException"/> if there are multiple entities with the given <paramref name="predicate"/>.
    /// </para>
    /// </summary>
    /// <param name="predicate">A condition to filter entities</param>
    /// <param name="includeDetails">Set true to include all children of this entity</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes many entities by the given <paramref name="predicate"/>.
    /// <para>
    /// Please note: This may cause major performance problems if there are too many entities returned for a
    /// given predicate and the database provider doesn't have a way to efficiently delete many entities.
    /// </para>
    /// </summary>
    /// <param name="predicate">A condition to filter entities</param>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    Task DeleteAsync(
        Expression<Func<TEntity, bool>> predicate,
        bool autoSave = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes all entities those fit to the given predicate.
    /// It directly deletes entities from database, without fetching them.
    /// Some features (like soft-delete, multi-tenancy and audit logging) won't work, so use this method carefully when you need it.
    /// Use the DeleteAsync method if you need to these features.
    /// </summary>
    /// <param name="predicate">A condition to filter entities</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns></returns>
    Task DeleteDirectAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default
    );
    /// <summary>Gets a list of all the entities.</summary>
    /// <param name="includeDetails">Set true to include all children of this entity</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>Entity</returns>
    Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default);

    /// <summary>Gets total count of all entities.</summary>
    Task<long> GetCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new entity.
    /// </summary>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <param name="entity">Inserted entity</param>
    Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple new entities.
    /// </summary>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <param name="entities">Entities to be inserted.</param>
    /// <returns>Awaitable <see cref="Task"/>.</returns>
    Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity.  只会标记根节点为Modified，更新当前根节点到传入的状态，对于嵌套的实体类，需要使用跟踪方式或手动标记Modified来进行更新。
    /// </summary>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <param name="entity">Entity</param>
    Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple entities.
    /// </summary>
    /// <param name="entities">Entities to be updated.</param>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>Awaitable <see cref="Task"/>.</returns>
    Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    /// <param name="entity">Entity to be deleted</param>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes multiple entities.
    /// </summary>
    /// <param name="entities">Entities to be deleted.</param>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>Awaitable <see cref="Task"/>.</returns>
    Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);
}

public interface IMoBasicRepository<TEntity, in TKey> : IMoBasicRepository<TEntity>
    where TEntity : class, IMoEntity<TKey>
{
    /// <summary>
    /// Deletes an entity by primary key.
    /// </summary>
    /// <param name="id">Primary key of the entity</param>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    Task DeleteAsync(TKey id, bool autoSave = false, CancellationToken cancellationToken = default);  //TODO: Return true if deleted

    /// <summary>
    /// Deletes multiple entities by primary keys.
    /// </summary>
    /// <param name="ids">Primary keys of the each entity.</param>
    /// <param name="autoSave">
    /// Set true to automatically save changes to database.
    /// This is useful for ORMs / database APIs those only save changes with an explicit method call, but you need to immediately save changes to the database.
    /// </param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>Awaitable <see cref="Task"/>.</returns>
    Task DeleteManyAsync(IEnumerable<TKey> ids, bool autoSave = false, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets an entity with given primary key.
    /// Throws <see cref="T:Volo.Abp.Domain.Entities.EntityNotFoundException" /> if can not find an entity with given id.
    /// </summary>
    /// <param name="id">Primary key of the entity to get</param>
    /// <param name="includeDetails">Set true to include all children of this entity</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>Entity</returns>
    Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity with given primary key or null if not found.
    /// </summary>
    /// <param name="id">Primary key of the entity to get</param>
    /// <param name="includeDetails">Set true to include all children of this entity</param>
    /// <param name="cancellationToken">A <see cref="T:System.Threading.CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>Entity or null</returns>
    Task<TEntity?> FindAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an entity exist or not
    /// </summary>
    /// <param name="id">Primary key of the entity</param>
    /// <returns></returns>
    Task<bool> ExistAsync(TKey id);
}
