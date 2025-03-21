using System.Text;

namespace BuildingBlocksPlatform.DataChannel.BuildInMiddlewares.DataTransformer;

/// <summary>
/// 默认采用UTF8进行转换
/// </summary>
/// <param name="encoding"></param>
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