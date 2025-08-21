using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Configuration.Providers;

public class JsonFileProviderConventions
{
    /// <summary>
    /// 配置文件Json格式化设置
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions { get; } =
        new()
        {
            WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() }
        };
}