using Microsoft.AspNetCore.Mvc;
using MoLibrary.DomainDrivenDesign.Attributes;
using MoLibrary.DomainDrivenDesign.AutoController.Settings;
using MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;
using MoLibrary.Repository.DtoInterfaces;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Repository.Exceptions;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DomainDrivenDesign.AutoCrud;


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
/// <inheritdoc/> <para>简化形式，1.该基类禁用修改与增加功能。2.需进一步禁用删除使用<see cref="IMoCrudDisableDelete"/> </para>
/// </summary>
public abstract class MoCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, TGetListInput, MoCrudDisableDto, MoCrudDisableDto,
        MoCrudDisableDto, TRepository>(repository) 
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
}

/// <summary>
/// <inheritdoc/> <para>简化形式，1.无需生成批量删除接口。2.单个输出与列表输出相同。3. 使用默认分页请求</para>
/// </summary>
public abstract class MoCrudAppService<TEntity, TEntityDto, TKey, TCreateInput, TUpdateInput, TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, MoCrudPageRequestDto, TCreateInput, TUpdateInput,
        MoCrudDisableDto, TRepository>(repository) 
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
}

/// <summary>
/// <inheritdoc/> <para>简化形式，1.无需生成批量删除接口。2.单个输出与列表输出相同。</para>
/// </summary>
public abstract class MoCrudAppService<TEntity, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput,
    TRepository>(TRepository repository)
    : MoCrudAppService<TEntity, TEntityDto, TEntityDto, TKey, TGetListInput, TCreateInput, TUpdateInput, MoCrudDisableDto, TRepository>(repository) 
    where TEntity : class, IMoEntity<TKey>
    where TEntityDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
}



/// <summary>
/// 自动CRUD接口基类。子类必须以设定的 <see cref="MoCrudControllerOption.CrudControllerPostfix"/> 结尾，否则无法自动注册。其余开头名字会自动生成为路由名，以小写单词短横线隔开。如UserListAppService：user-list
/// </summary>
/// <typeparam name="TEntity">实体类型，必须实现 <see cref="IMoEntity{TKey}"/> 接口</typeparam>
/// <typeparam name="TGetOutputDto">获取单个实体时的输出DTO类型，必须实现 <see cref="IMoEntityDto{TKey}"/> 接口</typeparam>
/// <typeparam name="TGetListOutputDto">获取实体列表时的输出DTO类型，必须实现 <see cref="IMoEntityDto{TKey}"/> 接口</typeparam>
/// <typeparam name="TKey">实体主键类型</typeparam>
/// <typeparam name="TGetListInput">获取实体列表时的输入参数类型</typeparam>
/// <typeparam name="TCreateInput">创建实体时的输入参数类型</typeparam>
/// <typeparam name="TUpdateInput">更新实体时的输入参数类型</typeparam>
/// <typeparam name="TBulkDeleteInput">批量删除实体时的输入参数类型</typeparam>
/// <typeparam name="TRepository">实体仓储类型，必须实现 <see cref="IMoRepository{TEntity, TKey}"/> 接口</typeparam>
/// <param name="repository">实体仓储实例</param>
public abstract class MoCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput,
    TUpdateInput, TBulkDeleteInput, TRepository>(TRepository repository) : 
    MoAbstractKeyCrudAppService<TEntity, TGetOutputDto, TGetListOutputDto, TKey, TGetListInput, TCreateInput, TUpdateInput>(repository), IMoCrudAppService
    where TEntity : class, IMoEntity<TKey>
    where TGetOutputDto : IMoEntityDto<TKey>
    where TGetListOutputDto : IMoEntityDto<TKey>
    where TRepository : IMoRepository<TEntity, TKey>
{
    /// <summary>
    /// 创建实体
    /// </summary>
    /// <param name="input">创建实体的输入参数</param>
    /// <returns>返回创建成功的响应结果</returns>
    /// <remarks>重写基类方法，使用标准响应格式返回结果</remarks>
    [OverrideService(-999)]
    public new virtual async Task<Res> CreateAsync(TCreateInput input)
    {
        var dto = await base.CreateAsync(input);
        return ResEntityCreateSuccess(dto);
    }

    /// <summary>
    /// 删除指定ID的实体
    /// </summary>
    /// <param name="id">要删除的实体ID</param>
    /// <returns>返回删除成功的响应结果</returns>
    /// <remarks>重写基类方法，使用标准响应格式返回结果</remarks>
    [OverrideService(-999)]
    public new virtual async Task<Res> DeleteAsync(TKey id)
    {
        await base.DeleteAsync(id);
        return ResEntityDeleteSuccess(id?.ToString() ?? "");
    }

    /// <summary>
    /// 批量删除实体
    /// </summary>
    /// <param name="input">包含要删除的实体ID集合的输入参数</param>
    /// <returns>返回批量删除的响应结果</returns>
    /// <remarks>如果输入参数实现了IHasRequestIds接口，则执行批量删除操作</remarks>
    public virtual async Task<Res> BulkDeleteAsync(TBulkDeleteInput input)
    {
        if (input is IHasRequestIds<TKey> keys)
        {
            //TODO 应支持软删除
            await repository.DeleteDirectAsync(p => keys.Ids.Contains(p.Id));
            return ResEntityDeleteSuccess(string.Join(",", keys.Ids));
        }

        return ResEntityDeleteFailed();
    }

    /// <summary>
    /// 更新指定ID的实体
    /// </summary>
    /// <param name="id">要更新的实体ID</param>
    /// <param name="input">更新实体的输入参数</param>
    /// <returns>返回更新成功的响应结果</returns>
    /// <remarks>
    /// 重写基类方法，使用标准响应格式返回结果
    /// 规范：TUpdateInput和TCreateInput不要继承Entity等基类
    /// </remarks>
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

    //TODO 移除此功能或迁移
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
