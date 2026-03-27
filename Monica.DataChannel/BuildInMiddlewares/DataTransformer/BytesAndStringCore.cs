using System.Text;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.BuildInMiddlewares.DataTransformer;

/// <summary>
/// Converts between byte arrays and strings by using UTF-8 by default.
/// </summary>
/// <param name="encoding">The encoding to use. When omitted, UTF-8 is used.</param>
public class BytesAndStringCore(Encoding? encoding = null) : BiDataTransformerMiddlewareBase<BytesAndStringCore, byte[], string>
{
    public override string Convert(byte[] data)
    {
        return data.ConvertToString(encoding ?? Encoding.UTF8);
    }

    public override byte[] Convert(string data)
    {
        return data.ConvertToBytes(encoding ?? Encoding.UTF8);
    }
}
