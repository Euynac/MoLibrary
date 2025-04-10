using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.MoResponse;

public static class MoResponseHelper
{
    /// <summary>
    /// [500] 微服务调用后需要检查，如果为False，应为服务调用出错，需要记录到微服务调用日志中去。接口调用异常由Mediator自动进行AOP，try catch进行日志记录
    /// </summary>
    public static bool IsServiceNormal(this IServiceResponse res) => res.Code != ResponseCode.InternalError;

    /// <summary>
    ///  [200] 代表请求正常处理
    /// </summary>
    public static bool IsOk(this IServiceResponse res) => res.Code == ResponseCode.Ok;

    /// <summary>
    /// 从远程调用请求结果 自动验证并附加信息
    /// </summary>
    /// <param name="res"></param>
    /// <param name="originInfo">HTTP等原始Response</param>
    /// <returns></returns>
    public static void AutoParseResponseFromOrigin(this IServiceResponse res, string originInfo)
    {
        if (IsNotValidResponse(res))
        {
            res.AppendExtraInfo("originRes", originInfo);
        }
    }

    /// <summary>
    ///  不是一个有效的请求，代表可能返回值并不符合此规范，应注意此情况进行特殊处理。
    /// </summary>
    public static bool IsNotValidResponse(this IServiceResponse res)
    {
        return res.Code == null;
    }
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
    /// 合并返回值信息，保全两个Res的信息，一般用于两个Res类型不一致的情况
    /// </summary>
    /// <param name="self"></param>
    /// <param name="response"></param>
    public static T MergeRes<T>(this T self, IServiceResponse response) where T : IServiceResponse
    {
        self.AppendExtraInfo("oriMsg", self.Message);
        self.AppendExtraInfo("oriCode", self.Code);
        response.ExtraInfo ??= new ExpandoObject();
        self.ExtraInfo!.Merge(response.ExtraInfo);
        self.Message = response.Message;
        self.Code = response.Code;
        //self.ExtraInfo = response.ExtraInfo;
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