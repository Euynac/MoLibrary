using System.Net.Http.Headers;
using System.Text;

namespace Monica.Core.Results.Models.Internal;

internal sealed class ResultEnvelopeCapturedContent(string content, bool isTruncated)
{
    public static ResultEnvelopeCapturedContent Empty { get; } = new(string.Empty, false);

    public string Content { get; } = content;

    public bool IsTruncated { get; } = isTruncated;

    public string DisplayContent => string.IsNullOrWhiteSpace(Content) ? "<Empty>" : Content;

    public static async Task<ResultEnvelopeCapturedContent> ReadAsync(HttpContent? content, int maxBytes)
    {
        if (content is null)
        {
            return Empty;
        }

        Stream? contentStream = null;
        ResultEnvelopeContentCaptureStream? captureStream = null;

        try
        {
            contentStream = await content.ReadAsStreamAsync();
            captureStream = new ResultEnvelopeContentCaptureStream(contentStream, maxBytes);
            var buffer = new byte[8192];

            while (await captureStream.ReadAsync(buffer.AsMemory(0, buffer.Length)) > 0)
            {
            }

            return captureStream.ToCapturedContent(content.Headers);
        }
        finally
        {
            if (captureStream is not null)
            {
                await captureStream.DisposeAsync();
            }
            else if (contentStream is not null)
            {
                await contentStream.DisposeAsync();
            }
        }
    }

    public static string DecodeBytes(byte[] bytes, HttpContentHeaders? headers)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var encoding = GetEncoding(headers);
        return encoding.GetString(bytes);
    }

    private static Encoding GetEncoding(HttpContentHeaders? headers)
    {
        var charset = headers?.ContentType?.CharSet;
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
