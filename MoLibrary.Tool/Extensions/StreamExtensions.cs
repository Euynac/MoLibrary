using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MoLibrary.Tool.Extensions;

public static class StreamExtensions
{
    public static byte[] GetAllBytes(this Stream stream)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using (var ms = stream.CreateMemoryStream())
        {
            return ms.ToArray();
        }
    }

    public static async Task<byte[]> GetAllBytesAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using (var ms = await stream.CreateMemoryStreamAsync(cancellationToken))
        {
            return ms.ToArray();
        }
    }

    public static Task CopyToAsync(this Stream stream, Stream destination, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return stream.CopyToAsync(
            destination,
            81920, //this is already the default value, but needed to set to be able to pass the cancellationToken
            cancellationToken
        );
    }

    public async static Task<MemoryStream> CreateMemoryStreamAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    public static MemoryStream CreateMemoryStream(this Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        memoryStream.Position = 0;
        return memoryStream;
    }
    public static async Task<string> ReadAsAsStringWithoutChangePosAsync(this Stream stream)
    {
        var reader = new StreamReader(stream);
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        var content = await reader.ReadToEndAsync();
        return content;
    }
    /// <summary>
    /// 读取流中的所有字节。如果流是MemoryStream，则直接返回ToArray()，否则将流复制到内存流中并返回ToArray()。
    /// Read all bytes in the stream. If the stream is MemoryStream, return ToArray() directly, otherwise copy the stream to the memory stream and return ToArray().
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static byte[] ReadAllBytes(this Stream stream)
    {
        if (stream is MemoryStream s) return s.ToArray();
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}