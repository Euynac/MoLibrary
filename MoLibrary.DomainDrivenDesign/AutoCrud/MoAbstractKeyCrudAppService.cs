using System.Linq.Dynamic.Core;
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
    protected IMoRepository<TEntity, TKey> Repository { get; } = repository;

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
    /// <returns>A paged response containing the mapped entity DTOs</returns>
    public virtual async Task<ResPaged<dynamic>> GetListAsync(TGetListInput input)
    {
        var query = await CreateFilteredQueryAsync(input);

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
                if (totalCount == 0) return new ResPaged<dynamic>(0, []);
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
            return new ResPaged<dynamic>(totalCount ?? selectedList.Count, selectedList, curPage, pageSize);
        }

        var entities = await finalEntityQuery.ToListAsync();

        //var entityDtos = await MapToGetListOutputDtosAsync(entities);//性能优化 使用Mapster代替

        //巨坑：ProjectToType中Dto若含有子表字段定义，会连带查出，无需主动Include。
        //20240422 Mapster暂不支持复杂类型ProjectToType
        //var entityDtos = await query.ProjectToType<TGetListOutputDto>(_mapper.Config).ToListAsync();

        var entityDtos = ObjectMapper.Map<List<TEntity>, List<TGetListOutputDto>>(entities);

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


        if ((await ApplyCustomActionToResponseListAsync(entityDtos)).IsFailed(out var error, out var data)) return error;
        var list = (IReadOnlyList<dynamic>) data;
        return new ResPaged<dynamic>(totalCount ?? list.Count, list, curPage, pageSize);
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
    /// <param name="entities">要处理的实体DTO列表</param>
    /// <returns>处理后的实体DTO列表，包装在Res结果中</returns>
    protected virtual async Task<Res<List<TGetListOutputDto>>> ApplyCustomActionToResponseListAsync(List<TGetListOutputDto> entities)
    {
        return entities;
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
    /// <returns>应用过滤后的查询</returns>
    protected virtual async Task<IQueryable<TEntity>> CreateFilteredQueryAsync(TGetListInput input)
    {
        var queryable = WithDetail() ? await Repository.WithDetailsAsync() : await Repository.GetQueryableAsync();
        queryable = queryable.AsNoTracking();

        queryable = ApplyListInclude(queryable);

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
    /// <returns>应用Include后的查询</returns>
    protected virtual IQueryable<TEntity> ApplyListInclude(IQueryable<TEntity> queryable)
    {
        return queryable;
    }
    /// <summary>
    /// 在GetList方法时应用Include
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
    /// It internally calls the <see cref="MapToGetOutputDto"/> by default.
    /// It can be overriden for custom mapping.
    /// Overriding this has higher priority than overriding the <see cref="MapToGetOutputDto"/>
    /// </summary>
    protected virtual Task<TGetOutputDto> MapToGetOutputDtoAsync(TEntity entity)
    {
        return Task.FromResult(MapToGetOutputDto(entity));
    }

    /// <summary>
    /// Maps <typeparamref name="TEntity"/> to <typeparamref name="TGetOutputDto"/>.
    /// It uses <see cref="IMoMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TGetOutputDto MapToGetOutputDto(TEntity entity)
    {
        return ObjectMapper.Map<TEntity, TGetOutputDto>(entity);
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
    /// Maps a list of <typeparamref name="TEntity"/> to <typeparamref name="TGetListOutputDto"/> objects.
    /// </summary>
    protected virtual async Task<List<TGetListOutputDto>> MapToGetListOutputDtosAsync(List<TEntity> entities)
    {
        var dtos = new List<TGetListOutputDto>();

        foreach (var entity in entities)
        {
            dtos.Add(MapToGetListOutputDto(entity));
        }

        return dtos;
    }

    /// <summary>
    /// Maps <typeparamref name="TEntity"/> to <typeparamref name="TGetListOutputDto"/>.
    /// It uses <see cref="IMoMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TGetListOutputDto MapToGetListOutputDto(TEntity entity)
    {
        return ObjectMapper.Map<TEntity, TGetListOutputDto>(entity);
    }
    #endregion

}
