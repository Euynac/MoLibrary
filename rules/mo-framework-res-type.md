# 统一接口返回模型Res

如需要查看完整定义，位于`Monica.Core/Results/Res.cs`

当前默认 JSON 返回字段为 `message`、`status`、`data`、`metadata`。
如需调整 Monica 内置结果模型的顶层 JSON 字段名，请通过 `Mo.AddResultEnvelope().UseResultFieldNames(...)` 配置，例如将 `message` 映射为 `msg`、将 `status` 映射为 `code`。
注意：这里配置的是“属性名”，仍会继续经过当前全局 `JsonSerializerOptions.PropertyNamingPolicy` 处理。
例如默认 camelCase 下，配置 `StatusCode` 最终会输出为 `statusCode`。
当前仅支持结果包顶层字段：`message`、`status`、`data`、`metadata`。

```csharp
Mo.AddResultEnvelope()
    .UseResultFieldNames(names =>
    {
        names.Message = "msg";
        names.Status = "code";
    });
```

## `Res<T>`泛型类型介绍

该类型定了许多隐式转换，支持以下隐式转换

- 当方法返回值是 `Res<T>` 时：
```cs
//返回错误
return "error desc"; // string => Res<T> ，Data 为 null，含有错误描述和Status 400代码
//等同于：
return Res.Fail("error desc"); // Res => Res<T> ，Data 为 null，含有错误描述和Status 400代码

//返回正确
return T; // 实例T => Res<T>，Status 200
```
- 当方法返回值是 `Res` 时：
```cs
//返回错误
return "error desc"; // string => Res，含有错误描述和Status 400代码
//等同于：
return Res.Fail("error desc")
    
//返回正确
return Res.Ok("正确描述");
```

- 当方法返回值是`Res<string>`时要注意：

```cs
//返回错误
return Res.Fail("error desc");

//返回正确
return Res.Ok<string>("Data string value");
```



### 最佳实践

以下为异步方法情况，同步类似。
#### 返回值为`Res<T>`时

使用隐式转换增强代码可读性：

```csharp
public override async Task<Res<ResponseUserCheck>> CheckUser(QueryUserCheck request,
    CancellationToken cancellationToken)
{
    var userInfo = await repo.GetUserInfo(request.Username);
    if (userInfo == null)
    {
        return $"用户名{request.Username}不存在";
    }
    return _mapper.Map<ResponseUserCheck>(userInfo); //直接返回ResponseUserCheck实例
}
```

使用`Res<T>`返回值方法时，判断响应是否正常、获取响应数据、错误等请使用以下模式（**无需定义新的result类型**）：

```cs
if ((await userManger.CheckUser(req)).IsFailed(out var error, out var data)) return error;
//此时data 为 ResponseUserCheck
```

#### 返回值为`Res`时

```cs
public override async Task<Res> Exist(User user,
    CancellationToken cancellationToken)
{
    if (!(await repo.Exist(user)))
    {
        return $"用户不存在";
    }
    return Res.Ok();
}
```

要在调用返回Res类型的方法后快速获取错误，请使用以下模式（**无需定义新的result类型**）：

```csharp
if ((await userManger.Exist(user)).IsFailed(out var error)) return error;
```
