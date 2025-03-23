using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.Pipeline;

public partial class DataContext
{
    /// <summary>
    /// 复制数据上下文元数据
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public DataContext CopyMetadata(DataContext data)
    {
        Metadata.Copy(data.Metadata);
        return this;
    }

    protected void AutoParseDataType()
    {
        if(Data == null) return;
        SpecifiedType = Data.GetType();
        if (SpecifiedType is { } type)
        {
            if (type == typeof(string))
            {
                DataType = EDataType.String;
            }
            else if (type == typeof(byte[]))
            {
                DataType = EDataType.Bytes;
            }
            else if (type.IsClass)
            {
                DataType = EDataType.Poco;
            }
        }
    }
}