using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MoLibrary.Tool.Extensions;

public static class GeneralExtensions
{
    public static string GetRelativePathInRunningPath(string relativePath)
    {
        return Path.Combine(GetRunningPath(), relativePath);
    }

    /// <summary>
    /// Get the running path of the current application.
    /// </summary>
    /// <returns></returns>
    public static string GetRunningPath()
    {
        return Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
    }

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

        var lineNumber = (int)(jsonException.LineNumber ?? 0);
        var bytePositionInLine = (int)(jsonException.BytePositionInLine ?? 0);

        var lines = originJson.Split(["\r\n", "\n" ], StringSplitOptions.None);

        
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
        var position = NormalizeCount(line, bytePosition);
        var start = Math.Max(0, position - contextWindow);
        var end = Math.Min(line.Length, position + contextWindow);

        var preview = line[start..end];

        var markedPreview = new StringBuilder(preview);
        markedPreview.Insert(Math.Min(position - start + 1, preview.Length), "<<< ERROR HERE <<<");

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
        var bytes = Encoding.UTF8.GetBytes(str);
        return bytes.Length != str.Length ? Encoding.UTF8.GetCharCount(bytes, 0, count) : count;
    }
}