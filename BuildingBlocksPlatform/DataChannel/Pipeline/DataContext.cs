using System.Dynamic;

namespace BuildingBlocksPlatform.DataChannel.Pipeline;

public partial class DataContext
{
    public DataContext(EDataSource entrance, EDataSource source, EDataOperation operation, object? data)
    {
        Entrance = entrance;
        Operation = operation;
        Data = data;
        Source = source;
        AutoParseDataType();
    }
    /// <summary>
    /// 消息数据进入入口。仅Inner或Outer。
    /// </summary>
    public EDataSource Entrance { get; set; }
    /// <summary>
    /// DataContext 诞生来源
    /// </summary>
    public EDataSource Source { get; set; }
    /// <summary>
    /// 数据操作类型
    /// </summary>
    public string OperationStr { get; private set; } = null!;
    public EDataOperation Operation
    {
        get => Enum.TryParse<EDataOperation>(OperationStr, true, out var operation)
            ? operation
            : EDataOperation.Custom;
        set => OperationStr = value.ToString().ToLower();
    }

    /// <summary>
    /// 元数据，Endpoint或中间件解析使用。
    /// </summary>
    public ExpandoObject Metadata { get; set; } = new();
    /// <summary>
    /// 数据对象
    /// </summary>
    public object? Data { get; set; }
    /// <summary>
    /// 数据类型
    /// </summary>
    public EDataType DataType { get; set; }
    /// <summary>
    /// 当<see cref="EDataType"/>为<see cref="EDataType.Poco"/>时指定具体类型
    /// </summary>
    public Type? SpecifiedType { get; set; }
    //TODO 处理ERROR
}

/// <summary>
/// 数据诞生来源或数据入口
/// </summary>
public enum EDataSource
{
    Inner,
    Outer,
    Middleware,
}

/// <summary>
/// 数据类型
/// </summary>
public enum EDataType
{
    Custom,
    Bytes,
    String,
    Poco
}

/// <summary>
/// 数据操作类型
/// </summary>
public enum EDataOperation
{
    Custom,
    #region Positive
    /// <summary>
    /// 主动获取
    /// </summary>
    Get,
    /// <summary>
    /// 主动推送
    /// </summary>
    Publish,
    #endregion

    #region Passive
    /// <summary>
    /// 被动响应
    /// </summary>
    Response,
    #endregion
}