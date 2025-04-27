# `Res<T>`泛型类型介绍

要快速返回错误响应，请使用以下模式：

```csharp
public override async Task<Res<ResponseUserCheck>> Handle(QueryUserCheck request,
    CancellationToken cancellationToken)
{
    var userInfo = await repo.GetUserInfo(request.Username);
    if (userInfo == null)
    {
        return $"用户名{request.Username}不存在";
    }
    return _mapper.Map<ResponseUserCheck>(userInfo);
}
```

要在调用返回Res类型的方法后快速获取错误，请使用以下模式：

```csharp
if ((await userManger.FillUserInfo(user)).IsFailed(out error)) return error;
```

## `Res<T>`的基本定义

```csharp
public record Res<T> : IServiceResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; }
    public T? Data { get; set; }

    public Res() { Message = ""; }
    public Res(T data) { Data = data; Code = ResponseCode.Ok; }
    public Res(string message, ResponseCode code) { Message = message; Code = code; }
    public Res(Exception e) { Message = $"服务出现异常：{e}"; Code = ResponseCode.InternalError; }

    public static implicit operator Res<T>(string res) => new(res, ResponseCode.BadRequest);
    public static implicit operator Res<T>(T data) => new(data);
    public static implicit operator string(Res<T> res) => res.Message ?? "";
}

public record Res : IServiceResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; }

    public Res(string message, ResponseCode code) { Message = message; Code = code; }

    public static Res Ok(string? hint = null) => new Res(hint ?? "", ResponseCode.Ok);
    public static Res Ok([StringSyntax("CompositeFormat")] string format, params object?[] args) => new Res(string.Format(format, args), ResponseCode.Ok);
    public static Res Fail(ResponseCode code, [StringSyntax("CompositeFormat")] string format, params object?[] args) => new Res(string.Format(format, args), code);
    public static Res Fail(string failDesc, ResponseCode code = ResponseCode.BadRequest) => new Res(failDesc, code);
    public static Res<T> Ok<T>(T data) => new Res<T>(data);
    public static Res<T> Create<T>(T data, ResponseCode code) => new Res<T>(data) { Code = code };
}

public static class ServiceResponseHelper
{
    public static bool IsOk(this IServiceResponse res) => res.Code == ResponseCode.Ok;
    public static bool IsFailed<T>(this Res<T> res, [NotNullWhen(true)] out Res? error, [NotNullWhen(false)] out T? data)
    {
        error = null;
        if (res.IsOk(out data)) return false;
        error = res.Message;
        return true;
    }
      public static bool IsFailed<T>(this Res<T> res, [NotNullWhen(true)] out Res? error)
    {
        error = null;
        if (res.IsOk()) return false;
        error = res.Message;
        return true;
    }
}