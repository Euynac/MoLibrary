using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Monica.AI.UI.UIChat.Providers.Browser;

internal sealed record BrowserChatHistoryEncodedDocument(
    IReadOnlyList<string> Chunks,
    string Sha256);

internal static class BrowserChatHistoryCodec
{
    internal const int DEFAULT_CHUNK_SIZE = 16 * 1024;

    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web);

    internal static BrowserChatHistoryEncodedDocument Encode<T>(
        T value,
        int chunkSize = DEFAULT_CHUNK_SIZE)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        var json = JsonSerializer.SerializeToUtf8Bytes(value, JSON_OPTIONS);
        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(json);
        }

        var compressed = compressedStream.ToArray();
        var payload = Convert.ToBase64String(compressed);
        var chunks = Enumerable.Range(0, (payload.Length + chunkSize - 1) / chunkSize)
            .Select(index => payload.Substring(index * chunkSize, Math.Min(chunkSize, payload.Length - index * chunkSize)))
            .ToArray();

        return new BrowserChatHistoryEncodedDocument(chunks, Convert.ToHexString(SHA256.HashData(compressed)));
    }

    internal static T Decode<T>(IReadOnlyList<string> chunks, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        var compressed = Convert.FromBase64String(string.Concat(chunks));
        var actualSha256 = Convert.ToHexString(SHA256.HashData(compressed));
        if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The chat history checksum does not match its payload.");
        }

        using var compressedStream = new MemoryStream(compressed);
        using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var jsonStream = new MemoryStream();
        gzip.CopyTo(jsonStream);

        return JsonSerializer.Deserialize<T>(jsonStream.ToArray(), JSON_OPTIONS)
               ?? throw new InvalidDataException("The chat history payload is empty or invalid.");
    }
}
