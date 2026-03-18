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
        WriteIndented = true
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
            return TryFormatJson(text) ?? text;
        }

        return SerializeObject(result);
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
}
