using System.Text.Json;

namespace MoLibrary.Core.UtilsAbstract;

public interface IGlobalJsonOption
{
    /// <summary>
    /// 全局唯一Json序列化设置
    /// </summary>
    public JsonSerializerOptions GlobalOptions { get; }
}