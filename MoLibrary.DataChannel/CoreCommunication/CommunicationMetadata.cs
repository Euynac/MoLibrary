namespace BuildingBlocksPlatform.DataChannel.CoreCommunication;

public abstract class CommunicationMetadata
{
    /// <summary>
    /// <inheritdoc cref="ECommunicationType"/>
    /// </summary>
    public ECommunicationType Type { get; set; } 
    /// <summary>
    /// 设置通信方向
    /// </summary>
    public EConnectionDirection Direction { get; set; }
    public abstract Type GetCommunicationCoreType();
    /// <summary>
    /// 验证Metadata有效性或根据创建后信息丰富Metadata。若有异常，直接抛出即可。
    /// </summary>
    public virtual void EnrichOrValidate()
    {

    }
}

public abstract class CommunicationMetadata<TCore> : CommunicationMetadata where TCore : CommunicationCore
{
    public override Type GetCommunicationCoreType() => typeof(TCore);
}

/// <summary>
/// 支持的通信方向
/// </summary>
public enum EConnectionDirection
{
    None,
    /// <summary>
    /// 可接收数据
    /// </summary>
    Input,
    /// <summary>
    /// 可发布数据
    /// </summary>
    Output,
    /// <summary>
    /// 双向通信，数据可出可入
    /// </summary>
    InputAndOutput
}

/// <summary>
/// 连接类型
/// </summary>
public enum ECommunicationType
{
    None,
    HTTP,
    TCP,
    UDP,
    MQ,
    SQL,
    Serial,
    Trigger
}