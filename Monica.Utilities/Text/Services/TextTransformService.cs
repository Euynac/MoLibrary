using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Monica.Utilities.Text.Models;

namespace Monica.Utilities.Text.Services;

/// <summary>
/// Performs escaped-text decoding and JSON formatting for the utilities toolbox.
/// </summary>
public sealed class TextTransformService
{
    private static readonly JsonSerializerOptions s_prettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions s_minifiedJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Executes the requested transformation.
    /// </summary>
    /// <param name="request">Transformation request describing the input text and target operation.</param>
    /// <returns>The transformed output and supporting metadata.</returns>
    public TextTransformResult Transform(TextTransformRequest request)
    {
        var normalized = request.Normalize();

        return normalized.Operation switch
        {
            TextTransformOperation.DecodeEscapedText => DecodeEscapedText(normalized),
            TextTransformOperation.FormatJson => RewriteJson(normalized, writeIndented: true),
            TextTransformOperation.MinifyJson => RewriteJson(normalized, writeIndented: false),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Operation, "Unsupported text transform operation.")
        };
    }

    private static TextTransformResult DecodeEscapedText(TextTransformRequest request)
    {
        var input = request.Input;
        var decodePasses = 0;
        var current = input;

        while (decodePasses < 5 && ContainsEscapedContent(current))
        {
            if (!TryDecodeOnce(current, out var decoded))
            {
                break;
            }

            if (string.Equals(decoded, current, StringComparison.Ordinal))
            {
                break;
            }

            current = decoded;
            decodePasses++;
        }

        var summary = decodePasses > 0
            ? $"Decoded {decodePasses} escape layer(s)."
            : "No JSON-style escape sequences were decoded. Output was left unchanged.";

        return new TextTransformResult(
            request.Operation,
            current,
            input.Length,
            current.Length,
            decodePasses,
            0,
            summary);
    }

    private static TextTransformResult RewriteJson(TextTransformRequest request, bool writeIndented)
    {
        var currentJson = request.Input;
        JsonDocument? document = null;
        var stringLayersRemoved = 0;

        try
        {
            document = JsonDocument.Parse(currentJson);

            while (stringLayersRemoved < 5
                   && document.RootElement.ValueKind == JsonValueKind.String
                   && LooksLikeStructuredJson(document.RootElement.GetString()))
            {
                var nestedJson = document.RootElement.GetString()!.Trim();
                document.Dispose();
                document = JsonDocument.Parse(nestedJson);
                currentJson = nestedJson;
                stringLayersRemoved++;
            }

            var output = JsonSerializer.Serialize(
                document.RootElement,
                writeIndented ? s_prettyJsonOptions : s_minifiedJsonOptions);

            var summary = writeIndented
                ? "Formatted JSON with readable Unicode output."
                : "Minified JSON with readable Unicode output.";

            if (stringLayersRemoved > 0)
            {
                summary += $" Removed {stringLayersRemoved} JSON string layer(s) first.";
            }

            return new TextTransformResult(
                request.Operation,
                output,
                request.Input.Length,
                output.Length,
                0,
                stringLayersRemoved,
                summary);
        }
        finally
        {
            document?.Dispose();
        }
    }

    private static bool TryDecodeOnce(string input, out string output)
    {
        var trimmed = input.Trim();

        try
        {
            if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            {
                output = JsonSerializer.Deserialize<string>(trimmed)
                         ?? string.Empty;
                return true;
            }

            output = JsonSerializer.Deserialize<string>(BuildJsonStringLiteral(input))
                     ?? string.Empty;
            return true;
        }
        catch (JsonException)
        {
            output = input;
            return false;
        }
    }

    private static bool ContainsEscapedContent(string input)
    {
        return input.Contains("\\u", StringComparison.OrdinalIgnoreCase)
               || input.Contains("\\n", StringComparison.Ordinal)
               || input.Contains("\\r", StringComparison.Ordinal)
               || input.Contains("\\t", StringComparison.Ordinal)
               || input.Contains("\\\"", StringComparison.Ordinal)
               || input.Contains("\\\\", StringComparison.Ordinal)
               || input.Contains("\\/", StringComparison.Ordinal);
    }

    private static bool LooksLikeStructuredJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string BuildJsonStringLiteral(string input)
    {
        var builder = new StringBuilder(input.Length + 2);
        builder.Append('"');

        foreach (var character in input)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        // Keep backslashes intact so JSON escape sequences are interpreted by the parser.
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
