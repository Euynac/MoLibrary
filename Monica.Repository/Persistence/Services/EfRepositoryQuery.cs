using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services;

internal sealed class EfRepositoryQuery<TEntity>(
    Func<Task<IQueryable<TEntity>>> queryFactory,
    object repository,
    IReadOnlyList<Func<IQueryable<TEntity>, IQueryable<TEntity>>> operations)
    : IRepositoryQuery<TEntity>
    where TEntity : class, IEntity
{
    public EfRepositoryQuery(Func<Task<IQueryable<TEntity>>> queryFactory, object repository)
        : this(queryFactory, repository, [])
    {
    }

    public IRepositoryQuery<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        return Add(query => query.Where(predicate));
    }

    public IRepositoryQuery<TEntity> Include(Expression<Func<TEntity, object?>> selector)
    {
        return Add(query => query.Include(selector));
    }

    public IRepositoryQuery<TEntity> Include(string navigationPath)
    {
        return Add(query => query.Include(navigationPath));
    }

    public IRepositoryQuery<TEntity> WithDetails()
    {
        if (repository is not IRepositoryDetailsConfigurator<TEntity> configurator)
        {
            throw new InvalidOperationException(
                $"Repository '{repository.GetType().FullName}' does not configure default details for entity '{typeof(TEntity).FullName}'. " +
                $"Implement {nameof(IRepositoryDetailsConfigurator<TEntity>)}<{typeof(TEntity).Name}> or use explicit Include calls.");
        }

        return Add(configurator.ApplyDetails);
    }

    public IRepositoryQuery<TEntity> AsNoTracking()
    {
        return Add(query => query.AsNoTracking());
    }

    public IRepositoryQuery<TEntity> AsTracking()
    {
        return Add(query => query.AsTracking());
    }

    public IRepositoryQuery<TEntity> IgnoreSoftDeleteFilter()
    {
        return IgnoreQueryFilters();
    }

    public IRepositoryQuery<TEntity> IgnoreQueryFilters()
    {
        return Add(query => query.IgnoreQueryFilters());
    }

    public IRepositoryQuery<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return Add(query => query.OrderBy(selector));
    }

    public IRepositoryQuery<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return Add(query => query.OrderByDescending(selector));
    }

    public IRepositoryQuery<TEntity> Skip(int count)
    {
        return Add(query => query.Skip(count));
    }

    public IRepositoryQuery<TEntity> Take(int count)
    {
        return Add(query => query.Take(count));
    }

    public async Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).ToListAsync(cancellationToken);
    }

    public async Task<TEntity[]> ToArrayAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).ToArrayAsync(cancellationToken);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<TEntity> FirstAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).FirstAsync(cancellationToken);
    }

    public async Task<TEntity> FirstAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).FirstAsync(predicate, cancellationToken);
    }

    public async Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).SingleOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).AnyAsync(predicate, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).CountAsync(cancellationToken);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).LongCountAsync(cancellationToken);
    }

    public async Task<PagedList<TEntity>> ToPagedListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be one or greater.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be one or greater.");
        }

        var query = await BuildAsync();
        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<TEntity>(items, totalCount, page, pageSize);
    }

    public Task<IQueryable<TEntity>> AsQueryableAsync()
    {
        return BuildAsync();
    }

    private EfRepositoryQuery<TEntity> Add(Func<IQueryable<TEntity>, IQueryable<TEntity>> operation)
    {
        return new EfRepositoryQuery<TEntity>(queryFactory, repository, [.. operations, operation]);
    }

    private async Task<IQueryable<TEntity>> BuildAsync()
    {
        var query = await queryFactory();
        foreach (var operation in operations)
        {
            query = operation(query);
        }

        return query;
    }
}
