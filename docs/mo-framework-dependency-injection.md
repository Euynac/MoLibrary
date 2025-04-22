# MoFramework依赖注入规则

MoFramework使用自动依赖注入机制，无需手动注册应用服务、领域服务和仓储。

## 自动注册原理

- 自动注册基于`IMoDependency`接口，位于`MoLibrary.DependencyInjection.AppInterfaces`命名空间
- 以下类型将被自动注册：
  - 实现`IMoRepository<TEntity, TKey>`接口的仓储类
  - 继承自`MoDomainService<TService>`的领域服务
  - 继承自`MoApplicationService<THandler, TRequest, TResponse>`的应用服务
  - 实现`IMoDependency`接口的其他类型

## 注册规则

1. **仓储**：
   - 仓储接口以自身接口类型注册
   - 仓储实现类以其接口（例如`IUserRepository`）和基础仓储接口（例如`IMoRepository<User, Guid>`）注册

2. **领域服务**：
   - 领域服务以自身类型注册（例如`DomainUser`）

3. **应用服务**：
   - 应用服务以自身类型注册（例如`CommandHandlerLogin`）

## 配置

在`Program.cs`中，只需一个配置即可启用自动依赖注入：

```csharp
builder.Services.AddMoDependencyInjectionDefaultProvider();
```

`RelatedAssemblies`参数指定要扫描的程序集，通常包括所有领域模型、仓储和服务。

## 注意事项

- 不要手动注册应用服务、领域服务和仓储，因为这可能导致重复注册
- 特殊类型（如IdGenerator、自定义服务等）仍需手动注册
- 确保实现类遵循命名约定，例如仓储接口`IUserRepository`对应实现类`UserRepository` 