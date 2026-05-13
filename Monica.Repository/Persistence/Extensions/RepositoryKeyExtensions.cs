using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;

namespace Monica.Repository.Persistence.Extensions;

/// <summary>
/// Provides keyed repository convenience operations that do not need to be part of the core repository contract.
/// </summary>
public static class RepositoryKeyExtensions
{
    /// <summary>
    /// Deletes all entities whose keys are included in <paramref name="ids"/>.
    /// </summary>
    public static async Task DeleteManyAsync<TEntity, TKey>(
        this IRepository<TEntity, TKey> repository,
        IEnumerable<TKey> ids,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity<TKey>
    {
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return;
        }

        var entities = await repository.Query()
            .Where(entity => idArray.Contains(entity.Id))
            .ToListAsync(cancellationToken);

        await repository.DeleteManyAsync(entities, cancellationToken);
    }
}
