using System.Linq.Dynamic.Core;
using System.Threading;
using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud.Interfaces;
using BuildingBlocksPlatform.DomainDrivenDesign.Interfaces;
using BuildingBlocksPlatform.Extensions;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.DynamicLinq;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.Repository;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.EntityInterfaces.Auditing;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Repository.Exceptions;
using MoLibrary.Tool.MoResponse;


namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud;


public abstract class MoAbstractKeyCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput, TUpdateInput>(IMoRepository<TEntity, TKey> repository) : MoApplicationService
    where TEntity : class, IMoEntity<TKey>
{
    protected IAutoModelDbOperator<TEntity> AutoModel =>
        ServiceProvider.GetRequiredService<IAutoModelDbOperator<TEntity>>();
    protected IMoRepository<TEntity, TKey> Repository { get; } = repository;

    #region 查

    public virtual async Task<TGetOutputDto> GetAsync(TKey id)
    {
        var entity = await GetEntityByIdAsync(id);

        return await MapToGetOutputDtoAsync(entity);
    }

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

        dynamicQuery = ApplyPaging(dynamicQuery, input);

        //如果已经应用了动态选择
        if (dynamicQuery is not IQueryable<TEntity> finalEntityQuery)
        {
            var selectedList = await dynamicQuery.ToDynamicListAsync();
            return new ResPaged<dynamic>(totalCount ?? selectedList.Count, selectedList);
        }

        var entities = await finalEntityQuery.ToListAsync();

        //var entityDtos = await MapToGetListOutputDtosAsync(entities);//性能优化 使用Mapster代替

        //巨坑：ProjectToType中Dto若含有子表字段定义，会连带查出，无需主动Include。
        //20240422 Mapster暂不支持复杂类型ProjectToType
        //var entityDtos = await query.ProjectToType<TGetListOutputDto>(_mapper.Config).ToListAsync();

        var entityDtos = ObjectMapper.Map<List<TEntity>, List<TGetListOutputDto>>(entities);
        if ((await ApplyCustomActionToResponseListAsync(entityDtos)).IsFailed(out var error, out var data)) return error;
        var list = (IReadOnlyList<dynamic>) data;
        return new ResPaged<dynamic>(totalCount ?? list.Count, list);
    }


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

    public virtual async Task<TGetOutputDto> CreateAsync(TCreateInput input)
    {
        var entity = MapToEntity(input);

        await Repository.InsertAsync(entity, autoSave: true);

        return await MapToGetOutputDtoAsync(entity);
    }


    #endregion

    #region 删




    public virtual async Task DeleteAsync(TKey id)
    {
        await DeleteByIdAsync(id);
    }
    protected virtual async Task DeleteByIdAsync(TKey id)
    {
        await Repository.DeleteAsync(id);
    }

    #endregion

    #region 改
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
    /// 应用分页。已扩展禁用分页功能。
    /// </summary>
    /// <param name="query"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    protected IQueryable ApplyPaging(IQueryable query, TGetListInput input)
    {
        if (input is IHasRequestPage pageRequest && input is IHasRequestSkipCount paged)

        {
            if (pageRequest is { DisablePage: true }) return query;
            if (paged.SkipCount == default || pageRequest.Page is not null)
            {
                pageRequest.Page ??= 1;
                paged.SkipCount = (pageRequest.Page.Value - 1) * paged.MaxResultCount;
            }
        }

        if (query is IQueryable<TEntity> entityQuery)
        {

            return input switch
            {
                IHasRequestSkipCount pagedResultRequest => entityQuery.Skip(pagedResultRequest.SkipCount ?? 0).Take(pagedResultRequest.MaxResultCount),
                IHasRequestLimitedResult limitedResultRequest => entityQuery.Take(limitedResultRequest.MaxResultCount),
                _ => entityQuery
            };
        }

        return input switch
        {
            IHasRequestSkipCount pagedResultRequest => query.Skip(pagedResultRequest.SkipCount ?? 0).Take(pagedResultRequest.MaxResultCount),
            IHasRequestLimitedResult limitedResultRequest => query.Take(limitedResultRequest.MaxResultCount),
            _ => query
        };
    }

    #endregion

    #region 自定义设置
    /// <summary>
    /// 对响应的实体Dto内容进行验证或进一步处理
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    protected virtual async Task<Res<List<TGetListOutputDto>>> ApplyCustomActionToResponseListAsync(List<TGetListOutputDto> entities)
    {
        return entities;
    }
    /// <summary>
    /// 设置自定义过滤条件
    /// </summary>
    /// <param name="input"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    protected virtual async Task<IQueryable<TEntity>> ApplyCustomFilterQueryAsync(TGetListInput input, IQueryable<TEntity> query)
    {
        return query;
    }
    /// <summary>
    /// 应用过滤器。已扩展统一查询模型及自定义过滤、客户端侧过滤功能。
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
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
    /// <param name="input"></param>
    /// <returns></returns>
    protected virtual Func<TEntity, bool>? ApplyCustomFilterQueryClientSideAsync(TGetListInput input)
    {
        return null;
    }
    #endregion
    /// <summary>
    /// 是否需要使用仓储层WithDetail方法
    /// </summary>
    /// <returns></returns>
    protected virtual bool WithDetail()
    {
        return false;
    }
    /// <summary>
    /// 在GetList方法时应用Include
    /// </summary>
    /// <param name="queryable"></param>
    /// <returns></returns>
    protected virtual IQueryable<TEntity> ApplyListInclude(IQueryable<TEntity> queryable)
    {
        return queryable;
    }
    /// <summary>
    /// 在GetList方法时应用Include
    /// </summary>
    /// <param name="queryable"></param>
    /// <returns></returns>
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
    /// It uses <see cref="IMoObjectMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TGetOutputDto MapToGetOutputDto(TEntity entity)
    {
        return ObjectMapper.Map<TEntity, TGetOutputDto>(entity);
    }

    /// <summary>
    /// Maps <typeparamref name="TCreateInput"/> to <typeparamref name="TEntity"/> to create a new entity.
    /// It uses <see cref="IMoObjectMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TEntity MapToEntity(TCreateInput createInput)
    {
        var entity = ObjectMapper.Map<TCreateInput, TEntity>(createInput);
        return entity;
    }


    /// <summary>
    /// Maps <typeparamref name="TUpdateInput"/> to <typeparamref name="TEntity"/> to update the entity.
    /// It uses <see cref="IMoObjectMapper"/> by default.
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
    /// It uses <see cref="IMoObjectMapper"/> by default.
    /// It can be overriden for custom mapping.
    /// </summary>
    protected virtual TGetListOutputDto MapToGetListOutputDto(TEntity entity)
    {
        return ObjectMapper.Map<TEntity, TGetListOutputDto>(entity);
    }
    #endregion

}
