using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json.Serialization;
using MoLibrary.Tool.General;

namespace MoLibrary.Tool.MoResponse;

/// <summary>
/// 统一响应模型，仅含有响应码和响应信息
/// </summary>
[DebuggerDisplay("{GetDebugValue()}")]
public class Res : IMoResponse
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
    /// <summary>
    /// 基于当前信息增加Data数据
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <returns></returns>
    public Res<T> WithData<T>(T data)
    {
        var res = new Res<T>(data)
        {
            Code = Code,
            ExtraInfo = ExtraInfo,
            Message = Message
        };
        return res;
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
/// 统一响应模型
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay("{GetDebugValue()}")]
public record Res<T> : IMoResponse
{
    public string? Message { get; set; }
    public ResponseCode? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? ExtraInfo { get; set; }
    /// <summary>
    /// Response的响应数据项
    /// </summary>
    public T? Data { get; set; }
    /// <summary>
    /// 创建空返回（并不代表成功）
    /// </summary>
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