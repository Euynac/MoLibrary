# 变量定义

> 变量表示特定上下文参数，并用`$`包围。

- `$BusinessFunctionName$` - 表示业务功能的名称。
- `$DomainName$` - 业务领域名称。必须使用PascalCase。
- `$APIRoute$` - 必须使用kebab-case。
- `$HandlerClassName$` - 表示处理程序类的名称。
- `$ResponseClassName$` - 表示响应类的名称。

# MoApplicationService规则

- 应用服务是业务领域内的API。相应的文件应命名为`CommandHandler$BusinessFunctionName$.cs`。
- 应用服务处理程序应由**请求类**、**响应类**和**处理程序类**组成。
- 处理程序类及其重写方法必须有完善的中文文档注释。

## 请求类

- 请求类必须实现`IMoRequest<TResponse>`，并应按照`Command$BusinessFunctionName$`或`Query$BusinessFunctionName$`格式命名。
- 文档应遵循`<inheritdoc cref="$HandlerClassName$"/>`格式。

## 响应类

- 响应类应按照`Response$BusinessFunctionName$`格式命名。
- 文档应遵循`<inheritdoc cref="$HandlerClassName$"/>` 接口响应格式。

## 处理程序类

- 处理程序类必须继承自`MoApplicationService<THandler, TRequest, TResponse>`。
- 处理程序类名称必须以`CommandHandler`或`QueryHandler`开头。
- 处理程序类应包含类似`[Route("api/v1/$DomainName$")]`的`Route`特性。
- 处理程序类必须重写`Handle`方法并包含`[HttpPost("$APIRoute$")]`特性。
- 处理程序类在需要依赖注入时应使用[primary-constructor.mdc](mdc:Affilion/.cursor/rules/primary-constructor.mdc)。
- 确保命名和结构的一致性，以在整个项目中保持清晰和标准化。

### 处理程序类响应类型

- 响应类型必须是`Task<Res<$ResponseClassName$>>`，可参考[mo-framework-res-type.mdc](mdc:Affilion/.cursor/rules/mo-framework-res-type.mdc)

## 示例

```cs
/// <summary>
/// <inheritdoc cref="CommandHandler$BusinessFunctionName$"/>
/// </summary>
public record Command$BusinessFunctionName$  : IMoRequest<Response$BusinessFunctionName$>
{
    
}

/// <summary>
/// <inheritdoc cref="CommandHandler$BusinessFunctionName$"/> 接口响应
/// </summary>
public record Response$BusinessFunctionName$
{

}
/// <summary>
/// 
/// </summary>
[Route("api/v1/$DomainName$")]
public class CommandHandler$BusinessFunctionName$ : MoApplicationService<CommandHandler$BusinessFunctionName$, Command$BusinessFunctionName$, Response$BusinessFunctionName$> 
{
    [HttpPost("$APIRoute$")]
    public override Task<Res<Response$BusinessFunctionName$>> Handle(Command$BusinessFunctionName$ request, CancellationToken cancellationToken)
    {
        ...
    }
}
``` 