using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace MoLibrary.Core.GlobalJson.Interfaces;

public interface IGlobalJsonOption
{
    /// <summary>
    /// 全局唯一Json序列化设置
    /// </summary>
    public JsonSerializerOptions GlobalOptions { get; }

    /// <summary>
    /// 使用当前全局JsonNamePolicy进行处理
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    [return: NotNullIfNotNull("str")]
    public string? UsingJsonNamePolicy(string? str);
}