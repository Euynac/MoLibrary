



using System.Collections.Generic;
using System.Linq.Dynamic.Core;
using System.Runtime.CompilerServices;
using System.Threading;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Core.Features.MoMapper;
using MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;
using MoLibrary.Repository;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.EntityInterfaces.Auditing;
using MoLibrary.Repository.Exceptions;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.AutoCrud;

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
public abstract class MoAbstractKeyCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput, TUpdateInput>(IMoRepository<TEntity, TKey> repository) : MoApplicationService
    where TEntity : class, IMoEntity<TKey>
{
    /// <summary>
    /// Gets the auto model database operator for entity operations.
    /// </summary>
    protected IAutoModelDbOperator<TEntity> AutoModel =>
        ServiceProvider.GetRequiredService<IAutoModelDbOperator<TEntity>>();
    
    /// <summary>
    /// Gets the repository for entity operations.
    /// </summary>
    protected virtual IMoRepository<TEntity, TKey> Repository { get; } = repository;

    #region 查

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


        if (input is not IHasRequestPage { DisablePage: true }) //不分页则不需要Count
        {
            if (featureSetting?.ShouldJumpCount() is not true)
            {
                totalCount = await query.CountAsync();
                if (totalCount == 0) return new ListResult([]);
            }
        }

        query = ApplySorting(query, input);

        var dynamicQuery = (IQueryable) query;

        //转化为 IQueryable 使得可应用动态选择
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

        //如果已经应用了动态选择
        if (dynamicQuery is not IQueryable<TEntity> finalEntityQuery)
        {
            var selectedList = await dynamicQuery.ToDynamicListAsync();
            return new ListResult(selectedList)
            {
                TotalCounts = totalCount ?? selectedList.Count
            };
        }

        //var entities = await finalEntityQuery.ToListAsync();

        var entityDtos = await MapToGetListOutputDtosAsync<TCustomDto>(finalEntityQuery);

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
            CurrentPage = curPage
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
                result.PageSize);
        
        
        if ((await ApplyCustomActionToResponseListAsync(input, dtos)).IsFailed(out var error, out var data)) return error;
        return new ResPaged<dynamic>(result.TotalCounts, (IReadOnlyList<dynamic>) data, result.CurrentPage,
            result.PageSize);
    }

    /// <summary>
    /// Retrieves a stream of entities based on the provided input.
    /// Returns data as IAsyncEnumerable for memory-efficient processing of large datasets.
    /// This method bypasses traditional paging and streams data directly from the database.
    /// </summary>
    /// <param name="input">The input parameters for the list operation</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>An async enumerable of mapped entity DTOs</returns>
    public virtual async IAsyncEnumerable<TGetListOutputDto> GetListStreamAsync(
        TGetListInput input, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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

    #region 增

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

    #region 删

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

    #region 改
    
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

        //TODO 其实可以不需要Update，因为已经开了跟踪，直接SaveChange即可
        await Repository.UpdateAsync(entity, autoSave: true);

        return await MapToGetOutputDtoAsync(entity);
    }
    #endregion


    #region 排序

    /// <summary>
    /// Applies sorting to the query based on the input.
    /// Handles special sorting requirements for EF Core split queries.
    /// </summary>
    /// <param name="query">The entity query to apply sorting to</param>
    /// <param name="input">The input containing sorting parameters</param>
    /// <returns>The sorted query</returns>
    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, TGetListInput input)
    {
        //巨坑：如果使用了Include，目前EFCore8会自动使用AsSpiltQuery功能。
        //而若同时联合使用Skip或Take功能，则需要要求多个查询结果一致，否则会导致查不到或查询出错的情况（因为生成的多个SQL，排序结果不同）
        //因为Include后会自动OrderBy 主键Id，而由于Skip或Take导致的子查询不会自动应用OrderBy BUG？
        //要确保Order使得结果一致 参考:https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries#split-queries
        if (WithDetail() && input is IHasRequestPage { DisablePage: not true })
        {
            query = query.HasBeenOrdered(out var ordered) ? ordered.ThenByDescending(p => p.Id) : query.OrderByDescending(p => p.Id);
        }

        //巨坑：Linq中的OrderBy，当连续进行OrderBy时，具体效果由Provider翻译决定。如PgSQL就会应用最后一次的OrderBy，前面的OrderBy将会忽略。
        if (input is IHasRequestSorting sortedResultRequest && !sortedResultRequest.Sorting.IsNullOrWhiteSpace())
        {
            return query.HasBeenOrdered(out var ordered) ? ordered.ThenBy(sortedResultRequest.Sorting) : query.OrderBy(sortedResultRequest.Sorting);
        }

        //巨坑：如果分表后进行分页，不进行排序会导致数据顺序、数量错误
        //巨坑：分表后不支持Select后不存在字段的OrderBy，因为分表后是加载到内存进行OrderBy的。
        if (input is IHasRequestSelect select && select.HasUsingSelected() && Repository.IsShardingTable())
        {
            return query;
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


    #region 分页

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

            return (input switch
            {
                IHasRequestSkipCount pagedResultRequest => entityQuery.Skip(pagedResultRequest.SkipCount ?? 0).Take(pagedResultRequest.MaxResultCount),
                IHasRequestLimitedResult limitedResultRequest => entityQuery.Take(limitedResultRequest.MaxResultCount),
                _ => entityQuery
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

    #region 自定义设置

    /// <summary>
    /// 对响应的实体Dto内容进行验证或进一步处理
    /// </summary>
    /// <param name="input"></param>
    /// <param name="dtos">要处理的实体DTO列表</param>
    /// <returns>处理后的实体DTO列表，包装在Res结果中</returns>
    protected virtual async Task<Res<List<TGetListOutputDto>>> ApplyCustomActionToResponseListAsync(TGetListInput input,
        List<TGetListOutputDto> dtos)
    {
        return dtos;
    }
    /// <summary>
    /// 设置自定义过滤条件
    /// </summary>
    /// <param name="input">获取列表的输入参数</param>
    /// <param name="query">要应用过滤的查询</param>
    /// <returns>应用过滤后的查询</returns>
    protected virtual async Task<IQueryable<TEntity>> ApplyCustomFilterQueryAsync(TGetListInput input, IQueryable<TEntity> query)
    {
        return query;
    }

    /// <summary>
    /// 应用过滤器。已扩展统一查询模型及自定义过滤、客户端侧过滤功能。
    /// </summary>
    /// <param name="input">获取列表的输入参数</param>
    /// <param name="repository">给定仓储层，如历史仓储</param>
    /// <returns>应用过滤后的查询</returns>
    protected virtual async Task<IQueryable<TEntity>> CreateFilteredQueryAsync(TGetListInput input, IMoRepository<TEntity, TKey>? repository = null)
    {
        repository ??= Repository;
        var queryable = WithDetail() ? await repository.WithDetailsAsync() : await repository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        queryable = ApplyListInclude(queryable, input);

        queryable = await ApplyCustomFilterQueryAsync(input, queryable);

        if (input is IHasRequestFilter filterRequest)
        {
            if (!string.IsNullOrEmpty(filterRequest.Filter))
                queryable = AutoModel.ApplyFilter(queryable, filterRequest.Filter);
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
    /// 设置仅能客户端侧评估的自定义过滤条件。注意：Client Side评估会导致分页等功能无法在数据库执行，大幅降低效率
    /// </summary>
    /// <param name="input">获取列表的输入参数</param>
    /// <returns>客户端侧过滤函数，如果为null则不应用客户端过滤</returns>
    protected virtual Func<TEntity, bool>? ApplyCustomFilterQueryClientSideAsync(TGetListInput input)
    {
        return null;
    }
    #endregion
    /// <summary>
    /// 是否需要使用仓储层WithDetail方法
    /// </summary>
    /// <returns>如果需要使用WithDetail方法则返回true，否则返回false</returns>
    protected virtual bool WithDetail()
    {
        return false;
    }

    /// <summary>
    /// 在GetList方法时应用Include
    /// </summary>
    /// <param name="queryable">要应用Include的查询</param>
    /// <param name="input"></param>
    /// <returns>应用Include后的查询</returns>
    protected virtual IQueryable<TEntity> ApplyListInclude(IQueryable<TEntity> queryable, TGetListInput input)
    {
        return queryable;
    }
    /// <summary>
    /// 在Get查询详情时应用Include
    /// </summary>
    /// <param name="queryable">要应用Include的查询</param>
    /// <returns>应用Include后的查询</returns>
    protected virtual IQueryable<TEntity> ApplyInclude(IQueryable<TEntity> queryable)
    {
        return queryable;
    }

    #region Mapper

    /// <summary>
    /// Maps <typeparamref name="TEntity"/> to <typeparamref name="TGetOutputDto"/>.
    /// It uses <see cref="IMoMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual Task<TGetOutputDto> MapToGetOutputDtoAsync(TEntity entity)
    {
        var result = ObjectMapper.Map<TEntity, TGetOutputDto>(entity);
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
        var result = ObjectMapper.Map<TEntity, TCustomDto>(entity);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Maps <typeparamref name="TCreateInput"/> to <typeparamref name="TEntity"/> to create a new entity.
    /// It uses <see cref="IMoMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TEntity MapToEntity(TCreateInput createInput)
    {
        var entity = ObjectMapper.Map<TCreateInput, TEntity>(createInput);
        return entity;
    }


    /// <summary>
    /// Maps <typeparamref name="TUpdateInput"/> to <typeparamref name="TEntity"/> to update the entity.
    /// It uses <see cref="IMoMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual void MapToEntity(TUpdateInput updateInput, TEntity entity)
    {
        ObjectMapper.Map(updateInput, entity);
    }

    /// <summary>
    /// Maps a list of <typeparamref name="TEntity"/> to <typeparamref name="TCustomDto"/> objects.
    /// </summary>
    protected virtual async Task<List<TCustomDto>> MapToGetListOutputDtosAsync<TCustomDto>(IQueryable<TEntity> query)
    {
        //巨坑：ProjectToType中Dto若含有子表字段定义，会连带查出，无需主动Include。
        //20240422 Mapster暂不支持复杂类型ProjectToType
        //return await ObjectMapper.ProjectToType<TCustomDto>(query).ToListAsync();
        return ObjectMapper.Map<List<TEntity>, List<TCustomDto>>(await query.ToListAsync());
    }


    #endregion
    protected class ListResult(IReadOnlyList<dynamic> results)
    {
        public IReadOnlyList<dynamic> Results { get; set; } = results;
        public int TotalCounts { get; set; }
        public int? PageSize { get; set; }
        public int? CurrentPage { get; set; }
    }
}

