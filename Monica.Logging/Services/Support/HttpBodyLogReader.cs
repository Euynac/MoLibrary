using System.Text;
using Microsoft.AspNetCore.Http;

namespace Monica.Logging.Services.Support;

internal static class HttpBodyLogReader
{
    public static async Task<string?> ReadRequestBodyAsync(HttpRequest request, int limit)
    {
        request.Body.Position = 0;

        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 512,
            leaveOpen: true);

        string content;
        if (request.ContentLength is null || request.ContentLength > limit)
        {
            var buffer = new char[limit];
            var charsRead = await reader.ReadAsync(buffer, 0, limit);
            content =
                $"[Too large to display, only read part of it: {charsRead}/{request.ContentLength?.ToString() ?? "unknown"}]{Environment.NewLine}{new string(buffer, 0, charsRead)}";
        }
        else
        {
            content = await reader.ReadToEndAsync();
        }

        request.Body.Position = 0;
        return content;
    }

    public static async Task<string> ReadResponseBodyAsync(HttpResponse response, MemoryStream memoryStream, int limit)
    {
        var logBuilder = new List<string>
        {
            $"[Response] {response.StatusCode} {response.ContentType}",
            "[Body]"
        };

        memoryStream.Position = 0;

        using var reader = new StreamReader(
            memoryStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 512,
            leaveOpen: true);

        if (memoryStream.Length > limit)
        {
            var buffer = new char[limit];
            var charsRead = await reader.ReadAsync(buffer, 0, limit);
            logBuilder.Add(
                $"[Too large to display, only read part of it: {charsRead}/{memoryStream.Length}]{Environment.NewLine}{new string(buffer, 0, charsRead)}");
        }
        else
        {
            logBuilder.Add(await reader.ReadToEndAsync());
        }

        memoryStream.Position = 0;

        return string.Join(Environment.NewLine, logBuilder);
    }
}
