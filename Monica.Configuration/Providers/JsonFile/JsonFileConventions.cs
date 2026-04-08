using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Monica.Configuration.Providers.JsonFile;

public class JsonFileConventions
{
    /// <summary>
    /// JSON formatting options used by configuration files.
    /// </summary>
    public static JsonSerializerOptions JsonSerializerOptions { get; } =
        new()
        {
            WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

    /// <summary>
    /// Converts an object to <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="value">The object to convert.</param>
    /// <returns>The converted <see cref="JsonElement"/>. Returns the original value when conversion fails.</returns>
    public static object ToJsonElement(object? value)
    {
        if (value == null) return value!;
        
        try
        {
            // Serialize the value to JSON and parse it as JsonElement.
            var jsonString = JsonSerializer.Serialize(value, JsonSerializerOptions);
            var jsonDocument = JsonDocument.Parse(jsonString);
            return jsonDocument.RootElement;
        }
        catch
        {
            // If conversion fails, keep the original value.
            return value;
        }
    }
}
