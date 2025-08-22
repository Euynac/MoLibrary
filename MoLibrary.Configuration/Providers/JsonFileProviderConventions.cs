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

    /// <summary>
    /// 将对象转换为JsonElement
    /// </summary>
    /// <param name="value">要转换的对象</param>
    /// <returns>转换后的JsonElement，如果转换失败则返回原值</returns>
    public static object ToJsonElement(object? value)
    {
        if (value == null) return value!;
        
        try
        {
            // 将所有value都序列化为JSON字符串，然后解析为JsonElement
            var jsonString = JsonSerializer.Serialize(value, JsonSerializerOptions);
            var jsonDocument = JsonDocument.Parse(jsonString);
            return jsonDocument.RootElement;
        }
        catch
        {
            // 如果转换失败，返回原值
            return value;
        }
    }
}