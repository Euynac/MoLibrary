using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json.Serialization;
using Monica.Core.Results.Abstractions;
using Monica.Tool.Diagnostics;
using Monica.Tool.Extensions;

// ReSharper disable once CheckNamespace
namespace Monica.Core.Results;

/// <summary>
/// Unified response model, only containing response code and response information
/// </summary>
[DebuggerDisplay("{GetDebugValue()}")]
public class Res : IResultEnvelope
{
    public string? Message { get; set; }

    public ResStatus Status { get; set; } = ResStatus.Unknown;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? Metadata { get; set; }

    /// <summary>
    /// Create exception return
    /// </summary>
    /// <param name="e"></param>
    public Res(Exception e)
    {
        Message = $"服务出现异常：{e}";
        Status = ResStatus.InternalError;
    }

    /// <summary>
    /// Create an empty return (does not mean success)
    /// </summary>
    public Res()
    {
        Message = "";
    }

    /// <summary>
    /// Create message return
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
    /// Create a successful response
    /// </summary>
    /// <param name="hint"></param>
    /// <returns></returns>
    public static Res Ok(string? hint = null)
    {
        return new Res(hint ?? "", ResStatus.Ok);
    }

    /// <summary>
    /// Create a successful response
    /// </summary>
    /// <param name="format"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static Res Ok([StringSyntax("CompositeFormat")] string format, params object?[] args)
    {
        return new Res(string.Format(format, args), ResStatus.Ok);
    }

    /// <summary>
    /// Create a failed response
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
    /// Create a failed response
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
    /// Returns a success or failure response depending on whether the data is empty
    /// </summary>
    /// <param name="data">Data to check</param>
    /// <param name="errorWhenNull">Error message when data is empty</param>
    /// <returns>Returns a successful response when the data is not empty, otherwise returns an error response</returns>
    public static Res<T> OkOrFailWhenNull<T>(T? data, string errorWhenNull) => data == null ? errorWhenNull : data;

    public static Res<T> Create<T>(T data, ResStatus status)
    {
        return new Res<T>(data)
        {
            Status = status
        };
    }

    /// <summary>
    /// Add Data based on current information
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
    /// Get Debug value
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
/// unified response model
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay("{GetDebugValue()}")]
public class Res<T> : IResultEnvelope
{
    public string? Message { get; set; }

    public ResStatus Status { get; set; } = ResStatus.Unknown;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandoObject? Metadata { get; set; }

    /// <summary>
    /// Response data items of Response
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Create an empty return (does not mean success)
    /// </summary>
    public Res()
    {
        Message = "";
    }

    /// <summary>
    /// Created successfully and returned
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
    /// Extract as new response data
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res(Res<T> res) => new(res.Message ?? "", res.Status)
    {
        Metadata = res.Metadata
    };

    /// <summary>
    /// Extract as new response data
    /// </summary>
    /// <param name="res"></param>
    public static implicit operator Res<T>(Res res) => new(res.Message ?? "", res.Status)
    {
        Metadata = res.Metadata
    };

    /// <summary>
    /// Get inheritable error information
    /// </summary>
    /// <returns></returns>
    public Res ToRes() => this;

    /// <summary>
    /// Get Debug value
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
