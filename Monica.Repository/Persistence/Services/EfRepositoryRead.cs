using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Exceptions;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services;

internal class EfRepositoryRead<TEntity>(
    Func<Task<IQueryable<TEntity>>> queryFactory,
    object repository,
    bool tracking,
    IReadOnlyList<Func<IQueryable<TEntity>, IQueryable<TEntity>>> operations)
    : IRepositoryRead<TEntity>
    where TEntity : class, IEntity
{
    public EfRepositoryRead(Func<Task<IQueryable<TEntity>>> queryFactory, object repository)
        : this(queryFactory, repository, tracking: false, [])
    {
    }

    public virtual IRepositoryRead<TEntity> AsTracking()
    {
        return WithTracking(true);
    }

    public virtual IRepositoryRead<TEntity> AsNoTracking()
    {
        return WithTracking(false);
    }

    public IRepositoryRead<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        return Add(query => query.Where(predicate));
    }

    public IRepositoryRead<TEntity> Include(Expression<Func<TEntity, object?>> selector)
    {
        return Add(query => query.Include(selector));
    }

    public IRepositoryRead<TEntity> Include(string navigationPath)
    {
        return Add(query => query.Include(navigationPath));
    }

    public IRepositoryRead<TEntity> WithDetails()
    {
        if (repository is not IRepositoryDetailsConfigurator<TEntity> configurator)
        {
            throw new InvalidOperationException(
                $"Repository '{repository.GetType().FullName}' does not configure default details for entity '{typeof(TEntity).FullName}'. " +
                $"Implement {nameof(IRepositoryDetailsConfigurator<TEntity>)}<{typeof(TEntity).Name}> or use explicit Include calls.");
        }

        return Add(configurator.ApplyDetails);
    }

    public IRepositoryRead<TEntity> IgnoreSoftDeleteFilter()
    {
        return IgnoreQueryFilters();
    }

    public IRepositoryRead<TEntity> IgnoreQueryFilters()
    {
        return Add(query => query.IgnoreQueryFilters());
    }

    public IRepositoryRead<TEntity> OrderBy<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return Add(query => query.OrderBy(selector));
    }

    public IRepositoryRead<TEntity> OrderByDescending<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return Add(query => query.OrderByDescending(selector));
    }

    public IRepositoryRead<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return Add(query => AsOrdered(query).ThenBy(selector));
    }

    public IRepositoryRead<TEntity> ThenByDescending<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        return Add(query => AsOrdered(query).ThenByDescending(selector));
    }

    public IRepositoryRead<TEntity> Skip(int count)
    {
        return Add(query => query.Skip(count));
    }

    public IRepositoryRead<TEntity> Take(int count)
    {
        return Add(query => query.Take(count));
    }

    public async Task<List<TEntity>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).ToListAsync(cancellationToken);
    }

    public async Task<List<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await SingleOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(predicate, cancellationToken);
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity));
        }

        return entity;
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

    public async Task<int> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).CountAsync(predicate, cancellationToken);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).LongCountAsync(cancellationToken);
    }

    public async Task<long> LongCountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await (await BuildAsync()).LongCountAsync(predicate, cancellationToken);
    }

    public async Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await MaterializePageAsync(await BuildAsync(), page, pageSize, cancellationToken);
    }

    public async Task<PagedList<TEntity>> GetPagedListAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await MaterializePageAsync((await BuildAsync()).Where(predicate), page, pageSize, cancellationToken);
    }

    public Task<IQueryable<TEntity>> GetQueryableAsync()
    {
        return BuildAsync();
    }

    protected virtual EfRepositoryRead<TEntity> Add(Func<IQueryable<TEntity>, IQueryable<TEntity>> operation)
    {
        return new EfRepositoryRead<TEntity>(queryFactory, repository, tracking, [.. operations, operation]);
    }

    protected virtual EfRepositoryRead<TEntity> WithTracking(bool nextTracking)
    {
        return new EfRepositoryRead<TEntity>(queryFactory, repository, nextTracking, operations);
    }

    private static IOrderedQueryable<TEntity> AsOrdered(IQueryable<TEntity> query)
    {
        if (query is IOrderedQueryable<TEntity> orderedQuery)
        {
            return orderedQuery;
        }

        throw new InvalidOperationException(
            $"{nameof(ThenBy)} and {nameof(ThenByDescending)} require a preceding {nameof(OrderBy)} or {nameof(OrderByDescending)} call.");
    }

    protected async Task<IQueryable<TEntity>> BuildAsync()
    {
        var query = await queryFactory();
        query = tracking ? query.AsTracking() : query.AsNoTracking();

        foreach (var operation in operations)
        {
            query = operation(query);
        }

        return query;
    }

    private static async Task<PagedList<TEntity>> MaterializePageAsync(
        IQueryable<TEntity> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be one or greater.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be one or greater.");
        }

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<TEntity>(items, totalCount, page, pageSize);
    }
}
