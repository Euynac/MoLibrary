using System.Diagnostics;
using System.Dynamic;
using MoLibrary.Tool.General;

namespace MoLibrary.Tool.MoResponse;

[DebuggerDisplay("{GetDebugValue()}")]
public record ResError<T> : IMoResponse
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