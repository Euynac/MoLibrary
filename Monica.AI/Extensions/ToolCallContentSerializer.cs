using System.Text.Encodings.Web;
using System.Text.Json;
using Monica.AI.Models;

namespace Monica.AI.Extensions;

/// <summary>
/// Serializes tool call payloads into debug-friendly text.
/// </summary>
internal static class ToolCallContentSerializer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string? SerializeArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
        {
            return null;
        }

        return SerializeObject(arguments);
    }

    public static string? SerializeResult(object? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result is string text)
        {
            return FormatText(text);
        }

        if (result is JsonElement element)
        {
            return SerializeJsonElement(element);
        }

        if (result is JsonDocument document)
        {
            return SerializeJsonElement(document.RootElement);
        }

        return SerializeUnknownResult(result);
    }

    public static ToolCallStatus GetFinalStatus(string? exceptionMessage)
        => string.IsNullOrWhiteSpace(exceptionMessage)
            ? ToolCallStatus.Completed
            : ToolCallStatus.Failed;

    private static string SerializeObject(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, s_jsonOptions);
        }
        catch
        {
            return value.ToString() ?? string.Empty;
        }
    }

    private static string SerializeJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return FormatText(element.GetString());
        }

        return JsonSerializer.Serialize(element, s_jsonOptions);
    }

    private static string SerializeUnknownResult(object value)
    {
        var serialized = SerializeObject(value);
        return TryUnwrapSerializedString(serialized) ?? serialized;
    }

    private static string FormatText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return TryFormatJson(text) ?? text;
    }

    private static string? TryFormatJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, s_jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryUnwrapSerializedString(string serialized)
    {
        try
        {
            using var document = JsonDocument.Parse(serialized);
            if (document.RootElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return FormatText(document.RootElement.GetString());
        }
        catch
        {
            return null;
        }
    }
}
