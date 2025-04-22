# 变量定义

> 变量表示特定上下文参数，并用`$`包围。

- `$EntityName$`: 实体的名称。
- `$PrimaryKeyType$`: 实体主键的类型。
- `$DbContextName$`: 特定业务领域的DbContext名称。

# MoRepository规则

- 仓储负责数据访问，应遵循标准仓储模式。
- 业务实体仓储应包含：
  - **实体类**
  - **仓储接口**
  - **仓储实现类**



## 实体类

- 必须继承自`MoEntity<$EntityKeyType$>`（单一主键）或`MoEntity`（多主键）。

- 当需要配置实体时，可以使用`IHasEntitySelfConfig<$EntityKeyType$>`

示例：
```cs
public class User : MoEntity<Guid>, IHasEntitySelfConfig<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
    }
}
```


## 仓储接口

- 命名为`IRepository$EntityName$`。
- 扩展`IMoRepository<$EntityName$, $EntityKeyType$>`（单一主键）或`IMoRepository<$EntityName$>`（多主键）。
- 为复杂查询定义自定义方法（不属于基础仓储），并用**中文**进行文档注释。
- 对于简单的CRUD操作，使用基础仓储方法。

示例：

```cs
public interface IRepositoryUser : IMoRepository<User, Guid>
{
    Task<User?> GetUserWithPermissions(string username, string passwordEncrypted);
}
```

## 仓储实现

- 命名为`Repository$EntityName$`。
- 继承自仓储接口和`MoRepository<$EntityName$, $EntityKeyType$>`（或多主键的`MoRepository<$EntityName$>`）。
- 使用[primary-constructor.mdc](mdc:Affilion/Affilion/Affilion/Affilion/Affilion/.cursor/rules/primary-constructor.mdc)进行依赖注入。注入`IDbContextProvider<$DbContextName$> dbContextProvider`。
- **异步**实现方法。

```cs
public class RepositoryUser(IDbContextProvider<UserDbContext> dbContextProvider)
    : MoRepository<UserDbContext, User, Guid>(dbContextProvider), IRepositoryUser
{
    public override IQueryable<User> DefaultDetailFunc(IQueryable<User> entities)
    {
        return entities.Include(p => p.OrganUnit).ThenInclude(d => d.Role).ThenInclude(r => r.Permissions);
    }

    public async Task<User?> GetUserWithPermissions(string username, string passwordEncrypted)
    {
        return await GetAsync(true, p => p.Password == passwordEncrypted && p.Username == username);
    }
}

```

# 内置仓储方法

对于简单的CRUD操作，使用基础仓储中的以下内置方法。无需在接口中定义它们。

```cs
// 删除操作
Task DeleteAsync(TKey id, bool autoSave = false, CancellationToken cancellationToken = default);
Task DeleteManyAsync(IEnumerable<TKey> ids, bool autoSave = false, CancellationToken cancellationToken = default);
Task DeleteAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default);
Task DeleteDirectAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
Task DeleteAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);
Task DeleteManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

// 获取操作
Task<TEntity> GetAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);
Task<TEntity?> FindAsync(TKey id, bool includeDetails = true, CancellationToken cancellationToken = default);
Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default);
Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = true, CancellationToken cancellationToken = default);
Task<IQueryable<TEntity>> WithDetailsAsync();
Task<IQueryable<TEntity>> WithDetailsAsync(params Expression<Func<TEntity, object>>[] propertySelectors);
Task<IQueryable<TEntity>> GetQueryableAsync();
Task<List<TEntity>> GetListAsync(Expression<Func<TEntity, bool>> predicate, bool includeDetails = false, CancellationToken cancellationToken = default);
Task<List<TEntity>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default);
Task<long> GetCountAsync(CancellationToken cancellationToken = default);

// 插入/更新操作
Task<TEntity> InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);
Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);
Task<TEntity> UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);
Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);
``` 




# 待完善

- [ ] 仅需定义仓储即可获得默认实现，如`IRepositoryUser`, 定义后其默认实现同`IMoRepository<TEntity, TKey>` （暂未实现）
- [ ] 直接使用`IMoRepository<TEntity, TKey>`也有其默认实现