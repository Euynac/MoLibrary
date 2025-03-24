using BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud.Interfaces;
using BuildingBlocksPlatform.Features;
using BuildingBlocksPlatform.Repository.DtoInterfaces;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Exceptions;
using BuildingBlocksPlatform.Repository.Interfaces;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoCrud;


/// <summary>
/// CRUD禁用删除接口标志
/// </summary>
public interface IMoCrudDisableDelete
{

}
public interface IMoCrudAppService
{
}

/// <summary>
/// <inheritdoc/> <para>该基类禁用修改与增加功能，需进一步禁用删除使用<see cref="IMoCrudDisableDelete"/> </para>
/// </summary>
public abstract class MoCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, TGetListInput, OurCrudDisableDto, OurCrudDisableDto,
        OurCrudDisableDto, TRepository>(repository) 
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
}

/// <summary>
/// <inheritdoc/>
/// </summary>
public abstract class MoCrudAppService<TEntity, TEntityDto, TKey, TCreateInput, TUpdateInput, TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, OurCrudPageRequestDto, TCreateInput, TUpdateInput,
        OurCrudDisableDto, TRepository>(repository) 
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
}

/// <summary>
/// <inheritdoc/>
/// </summary>
public abstract class MoCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput,
    TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput, OurCrudDisableDto, TRepository>(repository) 
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
}


/// <summary>
/// 自动CRUD接口基类。子类必须以AppService结尾，否则无法自动注册。其余开头名字会自动生成为路由名，以小写单词短横线隔开。如UserListAppService：user-list
/// </summary>
public abstract class MoCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput,
    TUpdateInput, TBulkDeleteInput, TRepository>(TRepository repository) : 
    MoAbstractKeyCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput, TUpdateInput>(repository), IMoCrudAppService
    where TEntity : class, IMoEntity<TKey>
    where TGetOutputDto : IMoEntityDto<TKey>
    where TGetListOutputDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
    [OverrideService(-999)]
    public new virtual async Task<Res> CreateAsync(TCreateInput input)
    {
        var dto = await base.CreateAsync(input);
        return ResEntityCreateSuccess(dto);
    }

    [OverrideService(-999)]
    public new virtual async Task<Res> DeleteAsync(TKey id)
    {
        await base.DeleteAsync(id);
        return ResEntityDeleteSuccess(id?.ToString() ?? "");
    }

    public virtual async Task<Res> BulkDeleteAsync(TBulkDeleteInput input)
    {
        if (input is IHasRequestIds<TKey> keys)
        {
            await repository.DeleteDirectAsync(p => keys.Ids.Contains(p.Id));
            return ResEntityDeleteSuccess(string.Join(",", keys.Ids));
        }

        return ResEntityDeleteFailed();
    }

    [OverrideService(-999)]
    public new virtual async Task<Res> UpdateAsync(TKey id, TUpdateInput input)
    {
        //规范：TUpdateInput和TCreateInput不要继承Entity等基类
        var dto = await base.UpdateAsync(id, input);
        return ResEntityUpdateSuccess(dto);
    }

    /// <summary>
    /// 生成的{id}路由规则是方法参数名为id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [OverrideService(-999)]
    public new virtual async Task<Res<TGetOutputDto>> GetAsync(TKey id)
    {
        try
        {
            return await base.GetAsync(id);
        }
        catch (EntityNotFoundException e)
        {
            return ResEntityNotFound(id!.ToString()!);
        }
    }

    //TODO 移除此功能或移至Our
    //TODO 此方法重写不同签名的需要增加POST标签，不会继承该标签
    [HttpPost]
    public virtual async Task<ResPaged<dynamic>> ListAsync(TGetListInput input)
    {
        return await GetListAsync(input);
    }
    #region 模板响应
    /// <summary>
    /// 实体名，用于模板响应
    /// </summary>
    protected virtual string? EntityName => null;
    /// <summary>
    /// 未找到给定ID实体
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    protected virtual Res ResEntityNotFound(string entityId)
    {
        if (EntityName is { } name)
        {
            return $"未找到相应的{name}";
        }
        return "未找到相应数据";
    }
    /// <summary>
    /// 实体更新成功
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    protected virtual Res ResEntityUpdateSuccess(TGetOutputDto dto)
    {
        if (EntityName is { } name)
        {
            return Res.Ok($"{name}更新成功");
        }
        return Res.Ok($"更新成功:{dto.Id}");
    }
    /// <summary>
    /// 实体更新成功
    /// </summary>
    /// <returns></returns>
    protected virtual Res ResEntityUpdateSuccess(string entityId)
    {
        if (EntityName is { } name)
        {
            return Res.Ok($"{name}更新成功");
        }
        return Res.Ok($"更新成功:{entityId}");
    }
    /// <summary>
    /// 实体更新失败
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    protected virtual Res ResEntityUpdateFailed(string entityId)
    {
        if (EntityName is { } name)
        {
            return $"{name}更新失败";
        }
        return "更新失败";
    }
    /// <summary>
    /// 实体新增成功
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>
    protected virtual Res ResEntityCreateSuccess(TGetOutputDto dto)
    {
        if (EntityName is { } name)
        {
            return Res.Ok($"{name}新增成功:{dto.Id}");
        }
        return Res.Ok($"新增成功:{dto.Id}");
    }
    /// <summary>
    /// 实体新增成功
    /// </summary>
    /// <returns></returns>
    protected virtual Res ResEntityCreateSuccess(string entityId)
    {
        if (EntityName is { } name)
        {
            return Res.Ok($"{name}新增成功:{entityId}");
        }
        return Res.Ok($"新增成功:{entityId}");
    }
    /// <summary>
    /// 实体新增失败
    /// </summary>
    /// <returns></returns>
    protected virtual Res ResEntityCreateFailed()
    {
        if (EntityName is { } name)
        {
            return $"{name}新增失败";
        }
        return "新增失败";
    }
    /// <summary>
    /// 实体删除成功
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    protected virtual Res ResEntityDeleteSuccess(string id)
    {
        if (EntityName is { } name)
        {
            return Res.Ok($"{name}删除成功:{id}");
        }
        return Res.Ok($"删除成功:{id}");
    }
    /// <summary>
    /// 实体删除失败
    /// </summary>
    /// <returns></returns>
    protected virtual Res ResEntityDeleteFailed()
    {
        if (EntityName is { } name)
        {
            return $"{name}删除失败";
        }
        return "删除失败";
    }
    #endregion
}
