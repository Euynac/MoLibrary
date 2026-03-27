using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json.Serialization;
using Monica.Tool.Extensions;
using Monica.Tool.General;

namespace Monica.Tool.Results;

/// <summary>
/// 统一响应模型，仅含有响应码和响应信息
/// </summary>
[DebuggerDisplay("{GetDebugValue()}")]
public class Res : IResultEnvelope
{
    [JsonPropertyName(ResJsonFieldNames.Message)]
    public string? Message { get; set; }

    [JsonPropertyName(ResJsonFieldNames.Status)]
    public ResStatus? Status { get; set; }

    [JsonPropertyName(ResJsonFieldNames.Metadata)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? Metadata { get; set; }

    /// <summary>
    /// 创建异常返回
    /// </summary>
    /// <param name="e"></param>
    public Res(Exception e)
    {
        Message = $"服务出现异常：{e}";
        Status = ResStatus.InternalError;
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
    /// <param name="status"></param>
    public Res(string message, ResStatus status)
    {
        Message = message;
        Status = status;
    }

    public static implicit operator Res(string res) => new(res, ResStatus.BadRequest);

    public static implicit operator string(Res res) => res.Message ?? "";

    /// <summary>
    /// 创建一个成功的响应
    /// </summary>
    /// <param name="hint"></param>
    /// <returns></returns>
    public static Res Ok(string? hint = null)
    {
        return new Res(hint ?? "", ResStatus.Ok);
    }

    /// <summary>
    /// 创建一个成功的响应
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static Res Ok([StringSyntax("CompositeFormat")] string format, params object?[] args)
    {
        return new Res(string.Format(format, args), ResStatus.Ok);
    }

    /// <summary>
    /// 创建一个失败的响应
    /// </summary>
    /// <param name="format"></param>
    /// <param name="status"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static Res Fail(ResStatus status, [StringSyntax("CompositeFormat")] string format, params object?[] args)
    {
        return new Res(string.Format(format, args), status);
    }

    /// <summary>
    /// 创建一个失败的响应
    /// </summary>
    /// <param name="failDesc"></param>
    /// <param name="status"></param>
    /// <returns></returns>
    public static Res Fail(string failDesc, ResStatus status = ResStatus.BadRequest)
    {
        return new Res(failDesc, status);
    }

    public static Res<T> Ok<T>(T data)
    {
        return new Res<T>(data);
    }

    /// <summary>
    /// 根据数据是否为空返回成功或失败响应
    /// </summary>
    /// <param name="data">要检查的数据</param>
    /// <param name="errorWhenNull">数据为空时的错误信息</param>
    /// <returns>数据不为空时返回成功响应，否则返回错误响应</returns>
    public static Res<T> OkOrFailWhenNull<T>(T? data, string errorWhenNull) => data == null ? errorWhenNull : data;

    public static Res<T> Create<T>(T data, ResStatus status)
    {
        return new Res<T>(data)
        {
            Status = status
        };
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
            Status = Status,
            Metadata = Metadata,
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
        if (Metadata is not null)
        {
            return $"{Message}({Status})\n{Metadata.ToJsonString()!}";
        }

        return $"{Message}({Status})";
    }
}

/// <summary>
/// 统一响应模型
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay("{GetDebugValue()}")]
public record Res<T> : IResultEnvelope
{
    [JsonPropertyName(ResJsonFieldNames.Message)]
    public string? Message { get; set; }

    [JsonPropertyName(ResJsonFieldNames.Status)]
    public ResStatus? Status { get; set; }

    [JsonPropertyName(ResJsonFieldNames.Metadata)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? Metadata { get; set; }

    /// <summary>
    /// Response的响应数据项
    /// </summary>
    [JsonPropertyName(ResJsonFieldNames.Data)]
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
        Status = ResStatus.Ok;
    }

    public Res(string message, ResStatus status)
    {
        Message = message;
        Status = status;
    }

    public Res(Exception e)
    {
        Message = $"服务出现异常：{e}";
        Status = ResStatus.InternalError;
    }

    public static implicit operator Res<T>(string res) => new(res, ResStatus.BadRequest);

    public static implicit operator Res<T>(T data) => new(data);

    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(Res<T> res) => new(res.Message ?? "", res.Status ?? ResStatus.BadRequest)
    {
        Metadata = res.Metadata
    };

    /// <summary>
    /// 提取为新响应数据
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res<T>(Res res) => new(res.Message ?? "", res.Status ?? ResStatus.BadRequest)
    {
        Metadata = res.Metadata
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
        if (Metadata is not null)
        {
            return $"{Message}({Status}) Data: {Data?.ToJsonStringForce()?.LimitMaxLength(500, "...")}\n{Metadata.ToJsonString()?.LimitMaxLength(500, "...")}";
        }

        return $"{Message}({Status}) Data: {Data?.ToJsonStringForce()?.LimitMaxLength(500, "...")}";
    }
}
