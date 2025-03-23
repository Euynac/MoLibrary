using System.IO;

namespace MoLibrary.Tool.Extensions;

public static class StreamExtensions
{
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