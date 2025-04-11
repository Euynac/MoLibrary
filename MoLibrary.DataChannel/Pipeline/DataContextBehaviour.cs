using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// DataContext的行为扩展部分类
/// 包含数据上下文的辅助方法和自动处理逻辑
/// </summary>
public partial class DataContext
{
    /// <summary>
    /// 复制数据上下文元数据
    /// 将指定数据上下文的元数据复制到当前实例
    /// </summary>
    /// <param name="data">源数据上下文</param>
    /// <returns>当前数据上下文实例</returns>
    public DataContext CopyMetadata(DataContext data)
    {
        Metadata.Copy(data.Metadata);
        return this;
    }

    /// <summary>
    /// 自动解析数据类型
    /// 根据Data属性的实际类型自动设置DataType和SpecifiedType属性
    /// </summary>
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