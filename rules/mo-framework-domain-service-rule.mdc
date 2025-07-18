# MoDomainService规则

- 领域服务是领域内的核心业务逻辑服务。
- 您应该创建一个类似`Domain$BusinessFunctionName$.cs`的文件。
- 领域服务类及其方法应该有完善的中文文档注释。

## 领域服务类

- 领域服务类必须继承自`MoDomainService<TDomainService>`。
- 类名必须以`Domain`开头。
- 领域服务类应通过构造函数注入必要的依赖项，使用[primary-constructor.mdc](mdc:Affilion/.cursor/rules/primary-constructor.mdc)。

## 领域服务方法

- 领域服务方法应该有描述性命名，反映它们所封装的业务逻辑。
- 执行可能有错误消息的异步操作的方法应返回`Task<Res>`或`Task<Res<T>>`，而同步操作应返回`Res`或`Res<T>`。Res类型参考[mo-framework-res-type.mdc](mdc:Affilion/.cursor/rules/mo-framework-res-type.mdc)。
- 领域服务应独立于应用层封装业务逻辑。 