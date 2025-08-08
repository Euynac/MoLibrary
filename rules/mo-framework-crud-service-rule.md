# 变量定义

> 变量表示特定上下文参数，并用`$`包围。

- `$EntityName$`: 实体的名称。
- `$EntityDtoName$`: 实体DTO的名称。
- `$PrimaryKeyType$`: 实体主键的类型（例如，Guid, int, string）。
- `$GetListInputName$`: 用于检索列表的输入类名称（通常包括分页参数）。
- `$CreateInputName$`: 用于创建实体的输入类名称。
- `$UpdateInputName$`: 用于更新实体的输入类名称。
- `$RepositoryInterfaceName$`: 仓储接口的名称。

# MoCrudService规则

- CRUD服务为实体提供标准的创建、读取、更新、删除操作。
- CRUD服务必须以`CrudService`为后缀命名（例如，`UserCrudService`）。
- CRUD服务应该根据所需功能继承自`MoCrudAppService`基类之一。
- 新的公共方法将根据命名约定自动生成相应的API。始终使用`Res`或`Res<T>`作为返回类型。

## 基类选择

### 使用自定义创建和更新输入的基础CRUD
```cs
public class $EntityName$CrudService(
    $RepositoryInterfaceName$ repository, 
    /* 其他依赖项 */) : 
    MoCrudAppService<$EntityName$, $EntityDtoName$, $PrimaryKeyType$, $CreateInputName$, $UpdateInputName$, $RepositoryInterfaceName$>(
        repository)
{
    // 实现 
}
```

### 使用自定义列表输入的基础CRUD
```cs
public class $EntityName$CrudService(
    $RepositoryInterfaceName$ repository, 
    /* 其他依赖项 */) : 
    MoCrudAppService<$EntityName$, $EntityDtoName$, $PrimaryKeyType$, $GetListInputName$, $CreateInputName$, $UpdateInputName$, $RepositoryInterfaceName$>(
        repository)
{
    // 实现
}
```

## 常见可重写方法

- `CreateAsync(TCreateInput input)`: 重写以自定义实体创建逻辑。
- `UpdateAsync(TKey id, TUpdateInput input)`: 重写以自定义实体更新逻辑。
- `DeleteAsync(TKey id)`: 重写以自定义实体删除逻辑。
- `GetAsync(TKey id)`: 重写以自定义实体检索逻辑。
- `ListAsync(TGetListInput input)`: 重写以自定义实体列表检索。
- `ApplyListInclude(IQueryable<TEntity> queryable)`: 重写以在列表查询中包含相关实体。
- `EntityName`: 重写以为响应消息提供自定义实体名称。

## 响应类型

CRUD服务方法使用[mo-framework-res-type.mdc](mdc:MoLibrary/rules/en/mo-framework-res-type.mdc)中定义的`Res`和`Res<T>`类型：

- `CreateAsync`: 返回`Task<Res>`
- `UpdateAsync`: 返回`Task<Res>`
- `DeleteAsync`: 返回`Task<Res>`
- `GetAsync`: 返回`Task<Res<TGetOutputDto>>`
- `ListAsync`: 返回`Task<ResPaged<dynamic>>`

## 特性

- 使用`[Tags("实体描述")]`特性对CRUD服务进行分类。

## 示例

```cs

[Tags("组织单位(OrganUnit)")]
public class OrganUnitAppService(IRepositoryOrganUnit repository, DomainOrganUnitManager manager, IRepositoryUser userRepo) :
    MoCrudAppService<OrganUnit, DtoOrganUnit, long, CommandCreateOrganUnit, CommandUpdateOrganUnit,
        IRepositoryOrganUnit>(repository)
{
    protected override string? EntityName => "组织单位";

    public class UpdateMembersRequest
    {
        /// <summary>
        /// 用户Id列表
        /// </summary>
        public required List<Guid> Users { get; set; }
        /// <summary>
        /// 组织单位Id，为空代表移除用户组织单位
        /// </summary>
        public long? Id { get; set; }
    }

    /// <summary>
    /// 批量修改给定用户组织单位
    /// </summary>
    public async Task<Res> UpdateMembersAsync(UpdateMembersRequest request)
    {
        var id = request.Id;
        var members = request.Users;
        if (id != null)
        {
            var unit = await repository.GetAsync(true, id.Value);
            if (unit == null) return ResEntityNotFound(id.Value.ToString());
            var users = await (await userRepo.WithDetailsAsync(p=>p.OrganUnit!)).Where(p => members.Contains(p.Id)).ToListAsync();
            foreach (var user in users)
            {
                user.OrganUnit = unit;
            }

            await userRepo.UpdateManyAsync(users);
            return Res.Ok($"已将{users.Count}个用户加入组织单位");
        }
        else
        {
            var users = await (await userRepo.WithDetailsAsync(p => p.OrganUnit!)).Where(p => members.Contains(p.Id) && p.OrganUnit != null).ToListAsync(); 
            foreach (var user in users)
            {
                user.OrganUnit = null;
            }

            await userRepo.UpdateManyAsync(users);
            return Res.Ok($"已将{users.Count}个用户移除组织单位");
        }
      
    }
}
``` 