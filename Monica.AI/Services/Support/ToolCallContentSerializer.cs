using System.Text.Encodings.Web;
using System.Text.Json;
using Monica.AI.Models;

namespace Monica.AI.Services.Support;

/// <summary>
/// Serializes tool call payloads into debug-friendly text.
/// </summary>
internal static class ToolCallContentSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
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

    public static string? GetExceptionMessage(object? result, Exception? exception)
    {
        if (exception is not null)
        {
            return exception.ToString();
        }

        return result is ToolInvocationErrorResult errorResult
            ? $"{errorResult.ErrorType}: {errorResult.Message}"
            : TryExtractSerializedToolError(result, out var serializedError)
                ? serializedError
            : null;
    }

    public static ToolCallStatus GetFinalStatus(string? exceptionMessage, object? result = null)
        => string.IsNullOrWhiteSpace(exceptionMessage)
           && result is not ToolInvocationErrorResult
           && !TryExtractSerializedToolError(result, out _)
            ? ToolCallStatus.Completed
            : ToolCallStatus.Failed;

    private static string SerializeObject(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, _jsonOptions);
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

        return JsonSerializer.Serialize(element, _jsonOptions);
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
            return JsonSerializer.Serialize(document.RootElement, _jsonOptions);
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

    private static bool TryExtractSerializedToolError(object? result, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (result is not string text || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetProperty(root, "status", out var status)
                || !string.Equals(status.GetString(), "tool_error", StringComparison.Ordinal))
            {
                return false;
            }

            var errorType = TryGetProperty(root, "errorType", out var errorTypeProperty)
                ? errorTypeProperty.GetString()
                : null;
            var message = TryGetProperty(root, "message", out var messageProperty)
                ? messageProperty.GetString()
                : null;

            errorMessage = string.IsNullOrWhiteSpace(errorType)
                ? message ?? "Tool invocation failed."
                : $"{errorType}: {message}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        foreach (var jsonProperty in element.EnumerateObject())
        {
            if (string.Equals(jsonProperty.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = jsonProperty.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
