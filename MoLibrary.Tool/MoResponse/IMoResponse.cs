using System.Dynamic;

namespace MoLibrary.Tool.MoResponse;

/// <summary>
/// 响应类不要直接使用该接口实现，而是继承ServiceResponse。
/// </summary>
public interface IMoResponse
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
}