using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoLibrary.Core.GlobalJson.Converters;

/// <summary>
/// 自定义日期时间JSON转换器，使用全局配置的日期时间格式进行序列化和反序列化
/// </summary>
public class MoDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var input = reader.GetString();
        if (reader.TokenType == JsonTokenType.String)
        {
            foreach (var format in DefaultMoGlobalJsonOptions.DateTimeFormats)
            {
                if (DateTime.TryParseExact(input, format, null, DateTimeStyles.None, out var date))
                {
                    return DefaultMoGlobalJsonOptions.NormalizeInTime(date);
                }
            }

            if (DateTime.TryParse(input, out var defaultDate))
            {
                return DefaultMoGlobalJsonOptions.NormalizeInTime(defaultDate);
            }
        }

        return DefaultMoGlobalJsonOptions.NormalizeInTime(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(DefaultMoGlobalJsonOptions.NormalizeOutTime(value).ToString(DefaultMoGlobalJsonOptions.OutputDateTimeFormat));
    }
}

/// <summary>
/// 保持原始日期时间格式的JSON转换器，用于在全局使用MoDateTimeJsonConverter时，
/// 对标记了此转换器的属性保持默认的DateTime序列化和反序列化方式
/// </summary>
public class PreserveOriginalDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// 反序列化DateTime，使用默认的JSON反序列化逻辑
    /// </summary>
    /// <param name="reader">JSON读取器</param>
    /// <param name="typeToConvert">要转换的类型</param>
    /// <param name="options">JSON序列化选项</param>
    /// <returns>反序列化后的DateTime</returns>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var input = reader.GetString();
            if (DateTime.TryParse(input, out var result))
            {
                return result;
            }
        }

        return reader.GetDateTime();
    }

    /// <summary>
    /// 序列化DateTime，使用默认的JSON序列化格式
    /// </summary>
    /// <param name="writer">JSON写入器</param>
    /// <param name="value">要序列化的DateTime值</param>
    /// <param name="options">JSON序列化选项</param>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O")); // 使用ISO 8601格式
    }
}