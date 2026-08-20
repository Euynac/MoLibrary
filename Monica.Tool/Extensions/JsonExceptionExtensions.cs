using System.Text;
using System.Text.Json;

namespace Monica.Tool.Extensions;

/// <summary>
/// Provides helpers for extracting readable context from <see cref="JsonException"/> instances.
/// </summary>
public static class JsonExceptionExtensions
{
    /// <summary>
    /// Only support line and bytes number in int range.
    /// </summary>
    /// <param name="jsonException"></param>
    /// <param name="originJson"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string GetJsonErrorDetails(this JsonException jsonException, string originJson)
    {
        ArgumentNullException.ThrowIfNull(jsonException);
        ArgumentNullException.ThrowIfNull(originJson);

        var lineNumberValue = jsonException.LineNumber ?? 0;
        var bytePositionValue = jsonException.BytePositionInLine ?? 0;

        if (lineNumberValue is < 0 or > int.MaxValue || bytePositionValue is < 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(jsonException), "The JSON error position is outside the supported range.");
        }

        var lineNumber = (int)lineNumberValue;
        var bytePositionInLine = (int)bytePositionValue;

        var lines = originJson.Split(["\r\n", "\n"], StringSplitOptions.None);

        var errorDetails = new StringBuilder();
        errorDetails.AppendLine($"Error at Line {lineNumber}, Byte Position {bytePositionInLine}: {jsonException.Message}");
        errorDetails.AppendLine("Context Preview:");

        if (lineNumber + 1 > lines.Length)
        {
            errorDetails.AppendLine("Error: Invalid line number in the exception.");
            return errorDetails.ToString();
        }

        var lineWithError = lines[lineNumber];
        var preview = GetPreviewAroundBytesPosition(lineWithError, bytePositionInLine);

        errorDetails.AppendLine(preview);

        return errorDetails.ToString();
    }

    public static string GetPreviewAroundBytesPosition(string line, int bytePosition, int contextWindow = 20)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentOutOfRangeException.ThrowIfNegative(bytePosition);
        ArgumentOutOfRangeException.ThrowIfNegative(contextWindow);

        var position = NormalizeCount(line, bytePosition);
        var start = Math.Max(0, position - contextWindow);
        var end = Math.Min(line.Length, position + contextWindow);

        var preview = line[start..end];

        var markedPreview = new StringBuilder(preview);
        markedPreview.Insert(Math.Clamp(position - start, 0, preview.Length), "<<< ERROR HERE <<<");

        return markedPreview.ToString();
    }

    /// <summary>
    /// Normalize bytes count or character count to character count.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static int NormalizeCount(string str, int count)
    {
        ArgumentNullException.ThrowIfNull(str);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var consumedBytes = 0;
        var characterIndex = 0;
        while (characterIndex < str.Length)
        {
            var characterLength = char.IsSurrogatePair(str, characterIndex) ? 2 : 1;
            var encodedLength = Encoding.UTF8.GetByteCount(str.AsSpan(characterIndex, characterLength));
            if (consumedBytes + encodedLength > count) break;

            consumedBytes += encodedLength;
            characterIndex += characterLength;
        }

        return characterIndex;
    }
}
