using System.Text;
using Monica.DevOps.FileOps.Exceptions;
using Monica.Tool.General;

namespace Monica.DevOps.FileOps.Services.Support;

public sealed class FileOpsTextReadResult
{
    public string Content { get; init; } = string.Empty;

    public string EncodingName { get; init; } = "utf-8";

    public int LineCount { get; init; }
}

public class FileOpsTextInspector
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<FileOpsTextReadResult> ReadAsync(string fullPath, long maxBytes, CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > maxBytes)
        {
            throw FileOpsOperationException.TextFileTooLarge(fullPath, maxBytes.FormatBytes());
        }

        var buffer = new byte[fileInfo.Length];
        await using var stream = File.OpenRead(fullPath);
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (LooksBinary(buffer))
        {
            throw FileOpsOperationException.BinaryFilePreviewUnsupported(fullPath);
        }

        var encoding = ResolveDetectedEncoding(buffer, out var preambleLength);
        var content = encoding.GetString(buffer, preambleLength, buffer.Length - preambleLength);
        return new FileOpsTextReadResult
        {
            Content = content,
            EncodingName = encoding.WebName,
            LineCount = string.IsNullOrEmpty(content) ? 0 : content.Count(static ch => ch == '\n') + 1
        };
    }

    public Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            return Utf8NoBom;
        }

        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch
        {
            return Utf8NoBom;
        }
    }

    private static bool LooksBinary(IReadOnlyList<byte> buffer)
    {
        if (buffer.Count == 0)
        {
            return false;
        }

        var inspectLength = Math.Min(buffer.Count, 4096);
        var suspiciousBytes = 0;
        for (var i = 0; i < inspectLength; i++)
        {
            var value = buffer[i];
            if (value == 0)
            {
                return true;
            }

            if (value < 8 || (value > 13 && value < 32))
            {
                suspiciousBytes++;
            }
        }

        return suspiciousBytes > inspectLength / 20;
    }

    private static Encoding ResolveDetectedEncoding(byte[] buffer, out int preambleLength)
    {
        if (buffer.Length >= 3 &&
            buffer[0] == 0xEF &&
            buffer[1] == 0xBB &&
            buffer[2] == 0xBF)
        {
            preambleLength = 3;
            return Encoding.UTF8;
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            preambleLength = 2;
            return Encoding.Unicode;
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            preambleLength = 2;
            return Encoding.BigEndianUnicode;
        }

        preambleLength = 0;
        return Utf8NoBom;
    }
}
