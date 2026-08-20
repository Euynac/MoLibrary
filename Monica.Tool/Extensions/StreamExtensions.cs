namespace Monica.Tool.Extensions;

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
        cancellationToken.ThrowIfCancellationRequested();

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
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var memoryStream = new MemoryStream();
        try
        {
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }
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
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new NotSupportedException("The stream must support seeking to preserve its position.");
        }

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, leaveOpen: true);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
    /// <summary>
    /// Read all bytes in the stream. If the stream is a MemoryStream, ToArray() is returned directly, otherwise the stream is copied to the memory stream and ToArray() is returned.
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
