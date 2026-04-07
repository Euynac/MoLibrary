using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Microsoft.Extensions.DependencyInjection;
using Monica.AutoModel.Abstractions;
using Monica.Core.ObjectMapping.Abstractions;
using Monica.Core.Results;
using Monica.Repository;
using Monica.Repository.Entity.Abstractions;
using Monica.Repository.Entity.Abstractions.Auditing;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Exceptions;
using Monica.Repository.Persistence.Extensions;
using Monica.Repository.UnitOfWork.Abstractions;
using Monica.Tool.Extensions;
using Monica.WebApi.Abstractions;
using Monica.WebApi.AutoControllers.Abstractions;

namespace Monica.WebApi.AutoControllers.Services;

/// <summary>
/// Base abstract class that implements common CRUD operations for entities with a key.
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TGetOutputDto">The DTO type for Get operation results</typeparam>
/// <typeparam name="TGetListOutputDto">The DTO type for GetList operation results</typeparam>
/// <typeparam name="TKey">The entity key type</typeparam>
/// <typeparam name="TGetListInput">The input type for GetList operations</typeparam>
/// <typeparam name="TCreateInput">The input type for Create operations</typeparam>
/// <typeparam name="TUpdateInput">The input type for Update operations</typeparam>
public abstract class AbstractKeyCrudApplicationService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput, TUpdateInput>(
    IRepository<TEntity, TKey> repository) : ApplicationService
    where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// Gets the auto model database operator for entity operations.
    /// </summary>
    protected IAutoModelDbOperator<TEntity> AutoModel => CachedServiceProvider.GetRequiredService<IAutoModelDbOperator<TEntity>>();

    /// <summary>
    /// Gets the unit of work manager.
    /// </summary>
    protected IUnitOfWorkManager UnitOfWorkManager => CachedServiceProvider.GetRequiredService<IUnitOfWorkManager>();
    
    /// <summary>
    /// Gets the repository for entity operations.
    /// </summary>
    protected virtual IRepository<TEntity, TKey> Repository { get; } = repository;

    #region Query
    protected virtual bool DisableProjectToType => false;
    /// <summary>
    /// Retrieves an entity by its ID and maps it to a DTO.
    /// </summary>
    /// <param name="id">The ID of the entity to retrieve</param>
    /// <returns>The mapped entity DTO</returns>
    public virtual async Task<TGetOutputDto> GetAsync(TKey id)
    {
        var entity = await GetEntityByIdAsync(id);

        return await MapToGetOutputDtoAsync(entity);
    }

    /// <summary>
    /// Retrieves a paged list of entities based on the provided input.
    /// Supports dynamic filtering, sorting, paging, and selecting specific properties.
    /// </summary>
    /// <param name="input">The input parameters for the list operation</param>
    /// <param name="query"></param>
    /// <returns>A paged response containing the mapped entity DTOs</returns>
    protected virtual async Task<ListResult> InnerGetListAsync<TCustomDto>(TGetListInput input, IQueryable<TEntity> query)
    {
        int? totalCount = null;

        FeatureSetting? featureSetting = null;

        if (input is IHasRequestFeature feature && feature.GetSetting() is { } setting)
        {
            featureSetting = setting;
        }

        if (input is not IHasRequestPage { DisablePage: true }) // No count is needed when paging is disabled.
        {
            if (featureSetting?.ShouldJumpCount() is not true)
            {
                totalCount = await query.CountAsync();
                if (totalCount == 0) return new ListResult([]);
            }
        }

        query = ApplySorting(query, input);

        var dynamicQuery = (IQueryable) query;

        // Cast to IQueryable so dynamic projection can be applied.
        if (input is IHasRequestSelect select && select.HasUsingSelected())
        {
            if (!select.SelectColumns.IsNullOrWhiteSpace())
            {
                dynamicQuery = AutoModel.DynamicSelect(query, select.SelectColumns);
            }
            else if (!select.SelectExceptColumns.IsNullOrWhiteSpace())
            {
                dynamicQuery = AutoModel.DynamicSelectExcept(query, select.SelectExceptColumns);
            }

            if (featureSetting?.FeatureFlags?.HasFlag(ERequestFeature.Distinct) is true)
            {
                dynamicQuery = dynamicQuery.Distinct();
                totalCount = await dynamicQuery.CountAsync();
            }
        }

        var (pagedQueryable, curPage, pageSize) = ApplyPaging(dynamicQuery, input);
        dynamicQuery = pagedQueryable;

        // If dynamic projection has already been applied.
        if (dynamicQuery is not IQueryable<TEntity> finalEntityQuery)
        {
            var selectedList = await dynamicQuery.ToDynamicListAsync();
            return new ListResult(selectedList)
            {
                TotalCounts = totalCount ?? selectedList.Count
            };
        }

        var entityDtos = await MapToGetListOutputDtosAsync<TCustomDto>(finalEntityQuery);
        var cursor = entityDtos is { Count: > 0 } && entityDtos.Last() is IEntityDto<TKey> dto ? dto.Id?.ToString() : null;
        if (curPage != null && pageSize != null && entityDtos.FirstOrDefault() is IHasDtoSequenceNumber)
        {
            // Calculate the starting index for the current page
            var startIndex = (curPage - 1) * pageSize + 1;

            for (var i = 0; i < entityDtos.Count; i++)
            {
                if (entityDtos[i] is IHasDtoSequenceNumber sequenceDto)
                {
                    sequenceDto.Num = startIndex + i;
                }
            }
        }

        return new ListResult((IReadOnlyList<dynamic>) entityDtos)
        {
            TotalCounts = totalCount ?? entityDtos.Count,
            PageSize = pageSize,
            CurrentPage = curPage,
            Cursor = cursor
        };
    }
    /// <summary>
    /// Retrieves a paged list of entities based on the provided input.
    /// Supports dynamic filtering, sorting, paging, and selecting specific properties.
    /// </summary>
    /// <param name="input">The input parameters for the list operation</param>
    /// <returns>A paged response containing the mapped entity DTOs</returns>
    protected virtual async Task<ListResult> InnerGetListAsync(TGetListInput input)
    {
        var query = await CreateFilteredQueryAsync(input);
        return await InnerGetListAsync<TGetListOutputDto>(input, query);
    }

    /// <summary>
    /// Retrieves a paged list of entities based on the provided input.
    /// Supports dynamic filtering, sorting, paging, and selecting specific properties.
    /// </summary>
    /// <param name="input">The input parameters for the list operation</param>
    /// <returns>A paged response containing the mapped entity DTOs</returns>
    public virtual async Task<ResPaged<dynamic>> GetListAsync(TGetListInput input)
    {
        var result = await InnerGetListAsync(input);
        if (result.TotalCounts == 0) return new ResPaged<dynamic>(0, []);

        if (result.Results is not List<TGetListOutputDto> dtos)
            return new ResPaged<dynamic>(result.TotalCounts, result.Results, result.CurrentPage,
                result.PageSize, result.Cursor);
        
        if ((await ApplyCustomActionToResponseListAsync(input, dtos)).IsFailed(out var error, out var data)) return error;
        return new ResPaged<dynamic>(result.TotalCounts, (IReadOnlyList<dynamic>) data, result.CurrentPage,
            result.PageSize, result.Cursor);
    }

    /// <summary>
    /// Retrieves a stream of entities based on the provided input.
    /// Returns data as IAsyncEnumerable for memory-efficient processing of large datasets.
    /// This method bypasses traditional paging and streams data directly from the database.
    /// </summary>
    /// <param name="input">The input parameters for the list operation</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>An async enumerable of mapped entity DTOs</returns>
    [HttpPost]
    public virtual async IAsyncEnumerable<TGetListOutputDto> ListStreamAsync(
        TGetListInput input, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Important: IAsyncEnumerable loses AsyncLocal values; by the time execution reaches this method, the ambient state is already gone.
        using var uow = UnitOfWorkManager.Begin();
        var query = await CreateFilteredQueryAsync(input);
        
        await foreach (var dto in InnerGetListStreamAsync<TGetListOutputDto>(input, query, cancellationToken))
        {
            yield return dto;
        }
    }
    /// <summary>
    /// Internal implementation for streaming list retrieval with custom DTO type.
    /// Supports filtering, sorting, and incremental processing without loading entire result set into memory.
    /// </summary>
    /// <typeparam name="TCustomDto">The custom DTO type for the results</typeparam>
    /// <param name="input">The input parameters for the list operation</param>
    /// <param name="query">The pre-filtered query to stream from</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>An async enumerable of mapped custom DTOs</returns>
    protected virtual async IAsyncEnumerable<TCustomDto> InnerGetListStreamAsync<TCustomDto>(
        TGetListInput input, 
        IQueryable<TEntity> query, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Apply sorting for consistent results in streaming scenarios
        query = ApplySorting(query, input);

        // Handle dynamic selection for streaming
        if (input is IHasRequestSelect select && select.HasUsingSelected())
        {
            var dynamicQuery = !select.SelectColumns.IsNullOrWhiteSpace()
                ? AutoModel.DynamicSelect(query, select.SelectColumns)
                : AutoModel.DynamicSelectExcept(query, select.SelectExceptColumns!);

            await foreach (var item in (dynamicQuery as IAsyncEnumerable<dynamic>)!.WithCancellation(cancellationToken))
            {
                yield return (TCustomDto)(dynamic)item;
            }
            yield break;
        }

        // Apply streaming limit if specified (but not traditional paging)
        if (input is IHasRequestLimitedResult limitedResultRequest)
        {
            query = query.Take(limitedResultRequest.MaxResultCount);
        }

        // Stream entities and map them incrementally
        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return await MapToGetListOutputDtoStreamAsync<TCustomDto>(entity);
        }
    }

    /// <summary>
    /// Retrieves an entity by its ID with optional includes.
    /// Throws EntityNotFoundException if the entity doesn't exist.
    /// </summary>
    /// <param name="id">The ID of the entity to retrieve</param>
    /// <returns>The entity with the specified ID</returns>
    /// <exception cref="EntityNotFoundException">Thrown when an entity with the specified ID is not found</exception>
    protected virtual async Task<TEntity> GetEntityByIdAsync(TKey id)
    {
        var entity = await ApplyInclude(await Repository.GetQueryableAsync()).OrderBy(e => e.Id).FirstOrDefaultAsync(e => e.Id!.Equals(id));
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(TEntity), id);
        }
        return entity;
    }

    #endregion

    #region Create

    /// <summary>
    /// Creates a new entity from the input, saves it to the database, and returns the result as a DTO.
    /// </summary>
    /// <param name="input">The input used to create the entity</param>
    /// <returns>The newly created entity as a DTO</returns>
    public virtual async Task<TGetOutputDto> CreateAsync(TCreateInput input)
    {
        var entity = MapToEntity(input);

        await Repository.InsertAsync(entity, autoSave: true);

        return await MapToGetOutputDtoAsync(entity);
    }


    #endregion

    #region Delete

    /// <summary>
    /// Deletes an entity by its ID.
    /// </summary>
    /// <param name="id">The ID of the entity to delete</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    public virtual async Task DeleteAsync(TKey id)
    {
        await DeleteByIdAsync(id);
    }
    
    /// <summary>
    /// Deletes an entity by its ID using the repository.
    /// </summary>
    /// <param name="id">The ID of the entity to delete</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    protected virtual async Task DeleteByIdAsync(TKey id)
    {
        await Repository.DeleteAsync(id);
    }

    #endregion

    #region Update
    
    /// <summary>
    /// Updates an existing entity with the provided input and returns the updated entity as a DTO.
    /// </summary>
    /// <param name="id">The ID of the entity to update</param>
    /// <param name="input">The input containing the update data</param>
    /// <returns>The updated entity as a DTO</returns>
    public virtual async Task<TGetOutputDto> UpdateAsync(TKey id, TUpdateInput input)
    {
        var entity = await GetEntityByIdAsync(id);
        //TODO: Check if input has id different than given id and normalize if it's default value, throw ex otherwise
        MapToEntity(input, entity);

        // TODO: This update call may be unnecessary because change tracking is already enabled; SaveChanges alone might be enough.
        await Repository.UpdateAsync(entity, autoSave: true);

        return await MapToGetOutputDtoAsync(entity);
    }
    #endregion


    #region Sorting

    /// <summary>
    /// Applies sorting to the query based on the input.
    /// Handles special sorting requirements for EF Core split queries.
    /// </summary>
    /// <param name="query">The entity query to apply sorting to</param>
    /// <param name="input">The input containing sorting parameters</param>
    /// <returns>The sorted query</returns>
    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, TGetListInput input)
    {
        // Important: when Include is used, EF Core 8 may automatically switch to split queries.
        // If split queries are combined with Skip or Take, every generated SQL query must produce the same ordering;
        // otherwise rows can be missed or the query can fail because each query may return a different order.
        // Include already introduces ordering by the primary key, while subqueries created by Skip or Take may not.
        // Keep the ordering deterministic. See: https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries#split-queries
        if (WithDetail() && input is IHasRequestPage { DisablePage: not true })
        {
            query = query.HasBeenOrdered(out var ordered) ? ordered.ThenByDescending(p => p.Id) : query.OrderByDescending(p => p.Id);
        }

        // Important: repeated OrderBy calls are provider-dependent. For example, PostgreSQL applies only the last OrderBy and ignores earlier ones.
        if (input is IHasRequestSorting sortedResultRequest && !sortedResultRequest.Sorting.IsNullOrWhiteSpace())
        {
            return query.HasBeenOrdered(out var ordered) ? ordered.ThenBy(sortedResultRequest.Sorting) : query.OrderBy(sortedResultRequest.Sorting);
        }

        // Important: paginating sharded tables without ordering can produce incorrect ordering and row counts.
        // Important: after sharding, OrderBy cannot target fields removed by Select because ordering is performed in memory.
        if (input is IHasRequestSelect select && select.HasUsingSelected() && Repository.IsShardingTable())
        {
            return query;
        }

        if (input is IHasRequestKeysetPage requestKeysetPage && requestKeysetPage.HasUsingKeyset())
        {
            query = FilterByKeyset(query, requestKeysetPage.Cursor!);
        }

        return input is IHasRequestLimitedResult ? ApplyDefaultSorting(query) : query;
    }

    /// <summary>
    /// Applies default sorting to the query if no specific sorting is provided.
    /// Orders by creation time (if entity implements IHasCreationTime) and then by ID.
    /// </summary>
    /// <param name="query">The entity query to apply default sorting to</param>
    /// <returns>The sorted query</returns>
    protected virtual IQueryable<TEntity> ApplyDefaultSorting(IQueryable<TEntity> query)
    {
        if (query.HasBeenOrdered(out var ordered))
        {
            return typeof(TEntity).IsAssignableTo<IHasCreationTime>() ?
                ordered.ThenByDescending(e => ((IHasCreationTime) e).CreationTime).ThenByDescending(e => e.Id) :
                ordered.ThenByDescending(e => e.Id);
        }

        return typeof(TEntity).IsAssignableTo<IHasCreationTime>() ?
            query.OrderByDescending(e => ((IHasCreationTime) e).CreationTime).ThenByDescending(e => e.Id) :
            query.OrderByDescending(e => e.Id);
    }

    #endregion


    #region Paging

    /// <summary>
    /// Applies paging to the query based on the input.
    /// </summary>
    /// <param name="query">The query to apply paging to</param>
    /// <param name="input">The input containing paging parameters</param>
    /// <returns>A tuple containing the paged query, current page number, and page size</returns>
    protected (IQueryable, int? curPage, int? pageSize) ApplyPaging(IQueryable query, TGetListInput input)
    {
        int? curPage = null;
        int? pageSize = null;
        if (input is IHasRequestPage pageRequest && input is IHasRequestSkipCount paged)

        {
            if (pageRequest is { DisablePage: true }) return (query, curPage, pageSize);
            if (paged.SkipCount == default || pageRequest.Page is not null)
            {
                pageRequest.Page ??= 1;
                paged.SkipCount = (pageRequest.Page.Value - 1) * paged.MaxResultCount;
            }


            curPage = pageRequest.Page;
            pageSize = paged.MaxResultCount;
        }

        if (query is IQueryable<TEntity> entityQuery)
        {
            if(input is IHasRequestKeysetPage requestKeysetPage && requestKeysetPage.HasUsingKeyset())
            {
                return (input switch
                {
                    IHasRequestSkipCount pagedResultRequest => entityQuery.Take(pagedResultRequest.MaxResultCount),
                    IHasRequestLimitedResult limitedResultRequest => entityQuery.Take(limitedResultRequest.MaxResultCount),
                    _ => entityQuery
                }, curPage, pageSize);
            }

            return (input switch
            {
                IHasRequestSkipCount pagedResultRequest => entityQuery.Skip(pagedResultRequest.SkipCount ?? 0).Take(pagedResultRequest.MaxResultCount),
                IHasRequestLimitedResult limitedResultRequest => entityQuery.Take(limitedResultRequest.MaxResultCount),
                _ => entityQuery
            }, curPage, pageSize);
        }

        if (input is IHasRequestKeysetPage keysetPage && keysetPage.HasUsingKeyset())
        {
            return (input switch
            {
                IHasRequestSkipCount pagedResultRequest => query.Take(pagedResultRequest.MaxResultCount),
                IHasRequestLimitedResult limitedResultRequest => query.Take(limitedResultRequest.MaxResultCount),
                _ => query
            }, curPage, pageSize);
        }

        return (input switch
        {
            IHasRequestSkipCount pagedResultRequest => query.Skip(pagedResultRequest.SkipCount ?? 0).Take(pagedResultRequest.MaxResultCount),
            IHasRequestLimitedResult limitedResultRequest => query.Take(limitedResultRequest.MaxResultCount),
            _ => query
        }, curPage, pageSize);
    }

    #endregion

    #region Customization

    /// <summary>
    /// Validates or further processes the response DTO list before it is returned.
    /// </summary>
    /// <param name="input">The input used for the list query.</param>
    /// <param name="dtos">The entity DTOs to process.</param>
    /// <returns>The processed DTO list wrapped in a <see cref="Res{T}"/> result.</returns>
    protected virtual async Task<Res<List<TGetListOutputDto>>> ApplyCustomActionToResponseListAsync(TGetListInput input,
        List<TGetListOutputDto> dtos)
    {
        return dtos;
    }
    /// <summary>
    /// Applies custom filter conditions.
    /// </summary>
    /// <param name="input">The input used for the list query.</param>
    /// <param name="query">The query to filter.</param>
    /// <returns>The filtered query.</returns>
    protected virtual async Task<IQueryable<TEntity>> ApplyCustomFilterQueryAsync(TGetListInput input, IQueryable<TEntity> query)
    {
        return query;
    }

    /// <summary>
    /// Creates the filtered query, including the unified query model, custom filters, and client-side filters.
    /// </summary>
    /// <param name="input">The input used for the list query.</param>
    /// <param name="repository">The repository to query, such as a history repository.</param>
    /// <returns>The filtered query.</returns>
    protected virtual async Task<IQueryable<TEntity>> CreateFilteredQueryAsync(TGetListInput input, IRepository<TEntity, TKey>? repository = null)
    {
        repository ??= Repository;
        var queryable = WithDetail() ? await repository.WithDetailsAsync() : await repository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        queryable = ApplyListInclude(queryable, input);

        queryable = await ApplyCustomFilterQueryAsync(input, queryable);

        if (input is IHasRequestFilter filterRequest)
        {
            if (!string.IsNullOrEmpty(filterRequest.Filter))
            {
                var result = AutoModel.GetNormalizedResult(filterRequest.Filter);
                if (result.Context.Tokens.Any(p => p.FieldInfo?.ReflectionName == nameof(IHasSoftDelete.IsDeleted)))
                {
                    queryable = repository.DisableSoftDeleteFilter(queryable); // Important: this was observed to stop working with sharded tables.
                }
                queryable = AutoModel.ApplyFilter(queryable, result);
            }
            if (!string.IsNullOrEmpty(filterRequest.Fuzzy))
                queryable = AutoModel.ApplyFuzzy(queryable, filterRequest.Fuzzy, filterRequest.FuzzyColumns);
        }


        var clientSideMethod = ApplyCustomFilterQueryClientSideAsync(input);
        if (clientSideMethod != null)
        {
            queryable = (await queryable.AsAsyncEnumerable().Where(clientSideMethod).ToListAsync()).AsQueryable();
        }

        return queryable;
    }
    /// <summary>
    /// Defines custom filter conditions that can only be evaluated on the client side.
    /// Client-side evaluation prevents paging and related operations from running in the database and can significantly reduce performance.
    /// </summary>
    /// <param name="input">The input used for the list query.</param>
    /// <returns>The client-side filter function, or <see langword="null"/> to skip client-side filtering.</returns>
    protected virtual Func<TEntity, bool>? ApplyCustomFilterQueryClientSideAsync(TGetListInput input)
    {
        return null;
    }
    #endregion
    /// <summary>
    /// Determines whether the repository's <c>WithDetail</c> method should be used.
    /// </summary>
    /// <returns><see langword="true"/> if <c>WithDetail</c> should be used; otherwise, <see langword="false"/>.</returns>
    protected virtual bool WithDetail()
    {
        return false;
    }

    /// <summary>
    /// Applies Include clauses when executing <c>GetList</c>.
    /// </summary>
    /// <param name="queryable">The query to extend with Include clauses.</param>
    /// <param name="input">The input used for the list query.</param>
    /// <returns>The query after Include clauses have been applied.</returns>
    protected virtual IQueryable<TEntity> ApplyListInclude(IQueryable<TEntity> queryable, TGetListInput input)
    {
        return queryable;
    }
    /// <summary>
    /// Applies Include clauses when loading entity details for <c>Get</c>.
    /// </summary>
    /// <param name="queryable">The query to extend with Include clauses.</param>
    /// <returns>The query after Include clauses have been applied.</returns>
    protected virtual IQueryable<TEntity> ApplyInclude(IQueryable<TEntity> queryable)
    {
        return queryable;
    }

    protected virtual IQueryable<TEntity> FilterByKeyset(IQueryable<TEntity> queryable, string cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return queryable;

        var paramExpression = Expression.Parameter(typeof(TEntity), "a");
        var memberExpression = Expression.PropertyOrField(paramExpression, "Id");

        var cursorValue = (TKey) Convert.ChangeType(cursor, typeof(TKey));
        var constant = Expression.Constant(cursorValue, typeof(TKey));

        var lessThan = Expression.LessThan(memberExpression, constant);
        var lamada = Expression.Lambda<Func<TEntity, bool>>(lessThan, paramExpression);

        return queryable.Where(lamada);
    }
    #region Mapper

    /// <summary>
    /// Maps <typeparamref name="TEntity"/> to <typeparamref name="TGetOutputDto"/>.
    /// It uses <see cref="IObjectMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual Task<TGetOutputDto> MapToGetOutputDtoAsync(TEntity entity)
    {
        var result = Mapper.Map<TEntity, TGetOutputDto>(entity);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Maps a single <typeparamref name="TEntity"/> to <typeparamref name="TCustomDto"/> for streaming scenarios.
    /// This method is called for each entity during streaming operations.
    /// </summary>
    /// <typeparam name="TCustomDto">The target DTO type</typeparam>
    /// <param name="entity">The entity to map</param>
    /// <returns>The mapped DTO</returns>
    protected virtual Task<TCustomDto> MapToGetListOutputDtoStreamAsync<TCustomDto>(TEntity entity)
    {
        var result = Mapper.Map<TEntity, TCustomDto>(entity);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Maps <typeparamref name="TCreateInput"/> to <typeparamref name="TEntity"/> to create a new entity.
    /// It uses <see cref="IObjectMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TEntity MapToEntity(TCreateInput createInput)
    {
        var entity = Mapper.Map<TCreateInput, TEntity>(createInput);
        return entity;
    }


    /// <summary>
    /// Maps <typeparamref name="TUpdateInput"/> to <typeparamref name="TEntity"/> to update the entity.
    /// It uses <see cref="IObjectMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual void MapToEntity(TUpdateInput updateInput, TEntity entity)
    {
        Mapper.Map(updateInput, entity);
    }

    /// <summary>
    /// Maps a list of <typeparamref name="TEntity"/> to <typeparamref name="TCustomDto"/> objects.
    /// </summary>
    protected virtual async Task<List<TCustomDto>> MapToGetListOutputDtosAsync<TCustomDto>(IQueryable<TEntity> query)
    {
        // Important: if the DTO defines child-table fields, ProjectToType will query them automatically, so explicit Include is unnecessary.
        // As of 2024-04-22, Mapster does not support ProjectToType for complex types.

        if (!DisableProjectToType)
        {
            return await Mapper.ProjectToType<TCustomDto>(query).ToListAsync();
        }
        else
        {
            return Mapper.Map<List<TEntity>, List<TCustomDto>>(await query.ToListAsync());
        }
    }

    #endregion
    protected class ListResult(IReadOnlyList<dynamic> results)
    {
        public IReadOnlyList<dynamic> Results { get; set; } = results;
        public int TotalCounts { get; set; }
        public int? PageSize { get; set; }
        public int? CurrentPage { get; set; }

        public string? Cursor { get; set; }
    }
}

