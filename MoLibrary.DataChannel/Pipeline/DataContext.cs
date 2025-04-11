using System.Dynamic;

namespace MoLibrary.DataChannel.Pipeline;

/// <summary>
/// 数据上下文类
/// 用于在数据传输和处理过程中包装和携带数据及其元数据
/// 作为数据管道中的主要传输单元
/// </summary>
public partial class DataContext
{
    /// <summary>
    /// 初始化DataContext的新实例
    /// </summary>
    /// <param name="entrance">数据进入点</param>
    /// <param name="source">数据来源</param>
    /// <param name="operation">数据操作类型</param>
    /// <param name="data">数据内容</param>
    public DataContext(EDataSource entrance, EDataSource source, EDataOperation operation, object? data)
    {
        Entrance = entrance;
        Operation = operation;
        Data = data;
        Source = source;
        AutoParseDataType();
    }
    
    /// <summary>
    /// 消息数据进入入口
    /// 表示数据从哪个端点进入管道，仅Inner或Outer
    /// </summary>
    public EDataSource Entrance { get; set; }
    
    /// <summary>
    /// DataContext的诞生来源
    /// 表示数据初始创建的位置或组件
    /// </summary>
    public EDataSource Source { get; set; }
    
    /// <summary>
    /// 数据操作类型的字符串表示
    /// 内部用于存储操作类型的字符串形式
    /// </summary>
    public string OperationStr { get; private set; } = null!;
    
    /// <summary>
    /// 数据操作类型
    /// 表示对数据执行的操作类型，如获取、发布等
    /// </summary>
    public EDataOperation Operation
    {
        get => Enum.TryParse<EDataOperation>(OperationStr, true, out var operation)
            ? operation
            : EDataOperation.Custom;
        set => OperationStr = value.ToString().ToLower();
    }

    /// <summary>
    /// 元数据
    /// 用于存储额外的上下文信息，可由Endpoint或中间件进行解析和使用
    /// </summary>
    public ExpandoObject Metadata { get; set; } = new();
    
    /// <summary>
    /// 数据对象
    /// 实际传输的数据内容
    /// </summary>
    public object? Data { get; set; }
    
    /// <summary>
    /// 数据类型
    /// 指定Data属性中存储的数据类型
    /// </summary>
    public EDataType DataType { get; set; }
    
    /// <summary>
    /// 当DataType为Poco时指定的具体类型
    /// 用于类型安全的数据访问和转换
    /// </summary>
    public Type? SpecifiedType { get; set; }
    //TODO 处理ERROR
}

/// <summary>
/// 数据来源或数据入口枚举
/// 定义数据在系统中的位置或流向
/// </summary>
public enum EDataSource
{
    /// <summary>
    /// 内部端点
    /// 表示数据位于系统内部或来自内部组件
    /// </summary>
    Inner,
    
    /// <summary>
    /// 外部端点
    /// 表示数据位于系统外部或来自外部系统
    /// </summary>
    Outer,
    
    /// <summary>
    /// 中间件
    /// 表示数据由管道中的中间件组件创建或修改
    /// </summary>
    Middleware,
}

/// <summary>
/// 数据类型枚举
/// 定义DataContext中Data属性的数据类型
/// </summary>
public enum EDataType
{
    /// <summary>
    /// 自定义类型
    /// 表示未分类或特殊处理的数据类型
    /// </summary>
    Custom,
    
    /// <summary>
    /// 字节数组类型
    /// 表示原始的二进制数据
    /// </summary>
    Bytes,
    
    /// <summary>
    /// 字符串类型
    /// 表示文本数据
    /// </summary>
    String,
    
    /// <summary>
    /// POCO对象类型
    /// 表示纯旧CLR对象，需要配合SpecifiedType属性使用
    /// </summary>
    Poco
}

/// <summary>
/// 数据操作类型枚举
/// 定义对数据执行的操作种类
/// </summary>
public enum EDataOperation
{
    /// <summary>
    /// 自定义操作
    /// 表示未分类或特殊处理的操作类型
    /// </summary>
    Custom,
    
    #region Positive
    /// <summary>
    /// 主动获取
    /// 表示从数据源主动请求并获取数据
    /// </summary>
    Get,
    
    /// <summary>
    /// 主动推送
    /// 表示向目标主动发送数据
    /// </summary>
    Publish,
    #endregion

    #region Passive
    /// <summary>
    /// 被动响应
    /// 表示对请求的回应或响应
    /// </summary>
    Response,
    #endregion
}