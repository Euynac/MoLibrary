using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Net;
using System.Runtime.CompilerServices;
using BuildingBlocksPlatform.Interfaces;
using System.Text.Json.Serialization;
using BuildingBlocksPlatform.Extensions;
using System.Text;
using Google.Rpc;
using Koubot.Tool.General;

[assembly: InternalsVisibleTo("InfrastructurePlatform")]
namespace BuildingBlocksPlatform.SeedWork;
public static class ServiceResponseHelper
{
    /// <summary>
    /// [500] 微服务调用后需要检查，如果为False，应为服务调用出错，需要记录到微服务调用日志中去。接口调用异常由Mediator自动进行AOP，try catch进行日志记录
    /// </summary>
    internal static bool IsServiceNormal(this IServiceResponse res) => res.Code != ResponseCode.InternalError;

    /// <summary>
    ///  [200] 代表请求正常处理
    /// </summary>
    public static bool IsOk(this IServiceResponse res) => res.Code == ResponseCode.Ok;

    /// <summary>
    /// 接口设置额外信息(重复会覆盖)
    /// </summary>
    /// <param name="res"></param>
    /// <param name="name"></param>
    /// <param name="info"></param>
    public static T SetExtraInfo<T>(this T res, string name, object? info) where T : IServiceResponse
    {
        res.ExtraInfo ??= new ExpandoObject();
        res.ExtraInfo.Set(name, info);
        return res;
    }
    /// <summary>
    /// 接口增加额外信息(重复Name则增加后缀)
    /// </summary>
    /// <param name="res"></param>
    /// <param name="name"></param>
    /// <param name="info"></param>
    public static T AppendExtraInfo<T>(this T res, string name, object? info) where T : IServiceResponse
    {
        res.ExtraInfo ??= new ExpandoObject();
        res.ExtraInfo.Append(name, info);
        return res;
    }

    /// <summary>
    /// [not 200] 代表请求存在问题
    /// </summary>
    public static bool IsFailed<T>(this Res<T> res, [NotNullWhen(true)] out Res? error, [NotNullWhen(false)] out T? data)
    {
        error = null;
        if (res.IsOk(out data)) return false;
        error = res.Inherit();
        return true;
    }
    /// <summary>
    /// [not 200] 代表请求存在问题
    /// </summary>
    public static bool IsFailed<T>(this Res<T> res, [NotNullWhen(true)] out Res? error)
    {
        error = null;
        if (res.IsOk()) return false;
        error = res.Inherit();
        return true;
    }
    /// <summary>
    /// [not 200] 代表请求存在问题
    /// </summary>
    public static bool IsFailed<T>(this Res<T?> res, [NotNullWhen(true)] out Res? error, out T? data) where T : struct
    {
        error = null;
        if (res.IsOk(out data)) return false;
        error = res.Inherit();
        return true;
    }
    /// <summary>
    /// [not 200] 代表请求存在问题
    /// </summary>
    public static bool IsFailed<T>(this Res<T?> res, [NotNullWhen(true)] out Res? error) where T : struct
    {
        error = null;
        if (res.IsOk()) return false;
        error = res.Inherit();
        return true;
    }
    /// <summary>
    /// [not 200] 代表请求存在问题
    /// </summary>
    public static bool IsFailed(this Res res, [NotNullWhen(true)] out Res? error)
    {
        error = null;
        if (res.IsOk()) return false;
        error = res;
        return true;
    }
    /// <summary>
    /// [200] 代表请求正常处理
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="res"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool IsOk<T>(this Res<T?> res, out T? data) where T : struct
    {
        data = res.Data!;
        return res.IsOk();
    }

    /// <summary>
    /// [200] 代表请求正常处理
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="res"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool IsOk<T>(this Res<T> res, [NotNullWhen(true)] out T data)
    {
        data = res.Data!;
        return res.IsOk();
    }

    /// <summary>
    /// 请求正常处理，并提供成功描述。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="self"></param>
    /// <param name="hint"></param>
    /// <returns></returns>
    public static T Ok<T>(this T self, string hint) where T : IServiceResponse
    {
        self.Code = ResponseCode.Ok;
        self.Message = hint;
        return self;
    }

    /// <summary>
    /// 批量调用结果转为单次调用结果。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="responses"></param>
    /// <returns></returns>
    public static Res<List<T>> ToBulkRes<T>(this IEnumerable<Res<T>> responses)
    {
        var list = new List<T>();
        var result = new Res<List<T>>()
        {
            Code = ResponseCode.Ok,
            Message = ""
        };
        var sb = new StringBuilder();
        foreach (var res in from response in responses select response)
        {
            if (res.Message is { } msg)
            {
                sb.AppendLine(msg);
            }

            if (res.ExtraInfo is { } extraInfo)
            {
                result.ExtraInfo ??= new ExpandoObject();
                result.ExtraInfo.Append("bulk", extraInfo);
            }
            if (!res.IsOk(out var data) && result.Code == ResponseCode.Ok)
            {
                result.Code = res.Code;
            }
            list.Add(data);
        }

        result.Message = sb.ToString().TrimEnd();
        result.Data = list;
        return result;
    }

    /// <summary>
    /// 继承错误，没有错误则返回本身
    /// </summary>
    /// <param name="self"></param>
    /// <param name="response"></param>
    public static T InheritError<T>(this T self, IServiceResponse response) where T : IServiceResponse
    {
        if (response.IsOk() && response.IsServiceNormal()) return self;
        self.Message += $"\n继承错误：[{response.GetType().Name}] {response.Message}";
        self.Message = self.Message.TrimStart();
        self.Code = response.Code;
        return self;
    }
    /// <summary>
    /// 追加信息
    /// </summary>
    /// <param name="self"></param>
    /// <param name="message"></param>
    public static T AppendMsg<T>(this T self, string? message)
        where T : IServiceResponse
    {
        return Append(self, message, null);
    }
    /// <summary>
    /// 追加信息
    /// </summary>
    /// <param name="self"></param>
    /// <param name="message"></param>
    /// <param name="code"></param>
    private static T Append<T>(this T self, string? message, ResponseCode? code)
        where T : IServiceResponse
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            self.Message += $";{message}";
            self.Message = self.Message.TrimStart(';');
        }

        if (code != null)
        {
            self.Code = code;
        }
        return self;
    }
    /// <summary>
    /// 追加错误
    /// </summary>
    /// <param name="self"></param>
    /// <param name="message"></param>
    /// <param name="code"></param>
    public static T AppendError<T>(this T self, string? message, ResponseCode code = ResponseCode.BadRequest)
        where T : IServiceResponse
    {
        return Append(self, message, code);
    }

    /// <summary>
    /// 根据上层响应创建成功响应或失败响应
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    /// <typeparam name="TLastResponse"></typeparam>
    /// <param name="res"></param>
    /// <param name="okResponse"></param>
    /// <param name="fallback"></param>
    /// <returns></returns>
    public static async Task<TResponse> OkOrFallback<TLastResponse, TResponse>(this Task<TLastResponse> res,
        TResponse okResponse,
        TResponse? fallback = null) where TLastResponse : IServiceResponse
        where TResponse : class, IServiceResponse, new()
    {
        var lastRes = await res;
        return lastRes.IsOk() ? okResponse : (fallback ?? new TResponse()).AppendError(lastRes.Message);
    }

    /// <summary>
    /// 转换为接口返回
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public static IServiceResponse ToServiceResponse(this IServiceResponse response) => response;
    /// <summary>
    /// 转换为特定类型返回
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <returns></returns>
    public static T ToServiceResponse<T>(this IServiceResponse response) where T : IServiceResponse, new() => new()
    {
        Code = response.Code,
        Message = response.Message,
        ExtraInfo = response.ExtraInfo
    };
}
[DebuggerDisplay("{GetDebugValue()}")]
public record ResError<T> : IServiceResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; }
    public ExpandoObject? ExtraInfo { get; set; }
    public ResError(T? error, string errorMsg, ResponseCode code = ResponseCode.BadRequest, string? errorDataKey = null)
    {
        Message = errorMsg;
        Code = code;
        this.AppendExtraInfo(errorDataKey ?? "error", error);
    }
    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(ResError<T> res) => new(res.Message ?? "", res.Code ?? ResponseCode.BadRequest)
    {
        ExtraInfo = res.ExtraInfo
    };

    /// <summary>
    /// 获取Debug值
    /// </summary>
    /// <returns></returns>
    internal string GetDebugValue()
    {
        if (ExtraInfo is not null)
        {
            return $"{Message}({Code})\n{ExtraInfo.ToJsonString()!}";
        }

        return $"{Message}({Code})";
    }
}


/// <summary>
/// 统一响应模型
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay("{GetDebugValue()}")]
public record Res<T> : IServiceResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? ExtraInfo { get; set; }
    /// <summary>
    /// Response的响应数据项
    /// </summary>
    public T? Data { get; set; }

    public Res()
    {
        Message = "";
    }

    /// <summary>
    /// 创建成功返回
    /// </summary>
    /// <param name="data"></param>
    public Res(T data)
    {
        Data = data;
        Code = ResponseCode.Ok;
    }

    public Res(string message, ResponseCode code)
    {
        Message = message;
        Code = code;
    }

    public Res(Exception e)
    {
        Message = $"服务出现异常：{e}";
        Code = ResponseCode.InternalError;
    }

    public static implicit operator Res<T>((string msg, T data) res) => new(res.msg, ResponseCode.Ok) { Data = res.data };

    public static implicit operator Res<T>(string res) => new(res, ResponseCode.BadRequest);

    public static implicit operator Res<T>(T data) => new(data);

    public static implicit operator string(Res<T> res) => res.Message ?? "";

    public static implicit operator Res<T>((T? data, string errorWhenNull) res) => res.data == null ? res.errorWhenNull : res.data;

    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(Res<T> res) => new(res.Message ?? "", res.Code ?? ResponseCode.BadRequest)
    {
        ExtraInfo = res.ExtraInfo
    };

    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res<T>(Res res) => new(res.Message ?? "", res.Code ?? ResponseCode.BadRequest)
    {
        ExtraInfo = res.ExtraInfo
    };

    /// <summary>
    /// 获取可继承的错误信息
    /// </summary>
    /// <returns></returns>
    public Res Inherit() => this;

    /// <summary>
    /// 获取Debug值
    /// </summary>
    /// <returns></returns>
    internal string GetDebugValue()
    {
        if (ExtraInfo is not null)
        {
            return $"{Message}({Code})\n{ExtraInfo.ToJsonString()!}";
        }

        return $"{Message}({Code})";
    }
}

/// <summary>
/// 统一响应模型，仅含有响应码和响应信息
/// </summary>
[DebuggerDisplay("{GetDebugValue()}")]
public class Res : IServiceResponse
{
    public string? Message { get; set; }

    public ResponseCode? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? ExtraInfo { get; set; }

    /// <summary>
    /// 创建异常返回
    /// </summary>
    /// <param name="e"></param>
    public Res(Exception e)
    {
        Message = $"服务出现异常：{e}";
        Code = ResponseCode.InternalError;
    }

    /// <summary>
    /// 创建空返回（并不代表成功）
    /// </summary>
    public Res()
    {
        Message = "";
    }

    /// <summary>
    /// 创建消息返回
    /// </summary>
    /// <param name="message"></param>
    /// <param name="code"></param>
    public Res(string message, ResponseCode code)
    {
        Message = message;
        Code = code;
    }

    public static implicit operator Res((string msg, ResponseCode code) res) => new(res.msg, res.code);

    public static implicit operator Res(string res) => new(res, ResponseCode.BadRequest);

    public static implicit operator string(Res res) => res.Message ?? "";

    public static implicit operator Res((Exception e, string msg) res)
    {
        var result = new Res(res.e);
        return result.AppendError(res.msg, ResponseCode.InternalError);
    }


    /// <summary>
    /// 创建一个成功的响应
    /// </summary>
    /// <param name="hint"></param>
    /// <returns></returns>
    public static Res Ok(string? hint = null)
    {
        return new Res(hint ?? "", ResponseCode.Ok);
    }

    /// <summary>
    /// 创建一个成功的响应
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static Res Ok([StringSyntax("CompositeFormat")] string format, params object?[] args)
    {
        return new Res(string.Format(format, args), ResponseCode.Ok);
    }
    /// <summary>
    /// 创建一个失败的响应
    /// </summary>
    /// <param name="format"></param>
    /// <param name="code"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static Res Fail(ResponseCode code, [StringSyntax("CompositeFormat")] string format, params object?[] args)
    {
        return new Res(string.Format(format, args), code);
    }
    /// <summary>
    /// 创建一个失败的响应
    /// </summary>
    /// <param name="failDesc"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    public static Res Fail(string failDesc, ResponseCode code = ResponseCode.BadRequest)
    {
        return new Res(failDesc, code);
    }

    public static Res<T> Ok<T>(T data)
    {
        return new Res<T>(data);
    }

    public static Res<T> Create<T>(T data, ResponseCode code)
    {
        return new Res<T>(data)
        {
            Code = code
        };
    }

    public static ResError<T> CreateError<T>(T error, string? errorMsg = null, ResponseCode code = ResponseCode.BadRequest,
        string? errorDataKey = null)
    {
        return new ResError<T>(error, errorMsg ?? "Error occured. see extra info for detail.", code, errorDataKey);
    }

    public override string ToString()
    {
        return Message ?? "";
    }

    /// <summary>
    /// 获取Debug值
    /// </summary>
    /// <returns></returns>
    internal string GetDebugValue()
    {
        if (ExtraInfo is not null)
        {
            return $"{Message}({Code})\n{ExtraInfo.ToJsonString()!}";
        }

        return $"{Message}({Code})";
    }

}

/// <summary>
/// 响应类不要直接使用该接口实现，而是继承ServiceResponse。
/// </summary>
public interface IServiceResponse
{
    /// <summary>
    /// 响应信息
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 响应码
    /// </summary>
    public ResponseCode? Code { get; set; }

    /// <summary>
    /// 接口扩展Debug信息，如调用链信息等
    /// </summary>
    public ExpandoObject? ExtraInfo { get; set; }

    /// <summary>
    /// 获取响应码对应的HttpStatusCode
    /// </summary>
    /// <returns></returns>
    public HttpStatusCode? GetHttpStatusCode()
    {
        switch (Code)
        {
            case ResponseCode.Ok:
                return HttpStatusCode.OK;


            case ResponseCode.Unauthorized:
            case ResponseCode.RefreshTokenExpired:
            case ResponseCode.AccessTokenExpired:
                return HttpStatusCode.Unauthorized;


            case ResponseCode.Forbidden:
                return HttpStatusCode.Forbidden;


            case ResponseCode.ValidateError:
            case ResponseCode.BadRequest:
                return HttpStatusCode.BadRequest;


            case ResponseCode.InternalError:
                return HttpStatusCode.InternalServerError;


            case null:
            case ResponseCode.Unknown:
                return null;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}


/// <summary>
/// 统一分页响应模型
/// </summary>
/// <typeparam name="TDto"></typeparam>
public class ResPaged<TDto> : IServiceResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; } = ResponseCode.Ok;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? ExtraInfo { get; set; }


    public PageData Data { get; set; }

    public class PageData(int? sum, IReadOnlyList<TDto>? items)
    {
        public int? Sum { get; set; } = sum;
        public IReadOnlyList<TDto>? Items { get; set; } = items;
    }
    public ResPaged()
    {
        Data = new PageData(null, null);
    }
    public ResPaged(int sum, IReadOnlyList<TDto> items)
    {
        Data = new PageData(sum, items);
    }
    #region ABP支持
    //public static ResPaged<TDto> Convert(PagedResultDto<TDto> dto)
    //{
    //    return new ResPaged<TDto>((int) dto.TotalCount, dto.Items)
    //    {
    //        Code = ResponseCode.Ok
    //    };
    //}

    //public static implicit operator ResPaged<TDto>(PagedResultDto<TDto> dto) => Convert(dto);
    #endregion
    /// <summary>
    /// 获取可继承的错误信息
    /// </summary>
    /// <returns></returns>
    public Res Inherit() => this;


    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(ResPaged<TDto> res) => new(res.Message ?? "", res.Code ?? ResponseCode.BadRequest)
    {
        ExtraInfo = res.ExtraInfo
    };

    public static implicit operator ResPaged<TDto>(Res res) => new(0, [])
    {
        Message = res.Message, Code = res.Code, ExtraInfo = res.ExtraInfo
    };

    public static implicit operator ResPaged<TDto>(string res) => new(0, [])
    { Message = res, Code = ResponseCode.BadRequest };
}


/// <summary>
/// 通用返回码
/// </summary>
public enum ResponseCode
{
    Unknown = 0,
    Ok = 200,
    BadRequest = 400,
    /// <summary>
    /// 未登录
    /// </summary>
    Unauthorized = 401,
    /// <summary>
    /// 刷新Token失效
    /// </summary>
    RefreshTokenExpired = 452,
    /// <summary>
    /// 访问Token失效
    /// </summary>
    AccessTokenExpired = 453,
    /// <summary>
    /// 权限不足
    /// </summary>
    Forbidden = 403,
    ValidateError = 451,
    InternalError = 500,
}