using BuildingBlocksPlatform.DataChannel.CoreCommunication;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.Default;


/// <summary>
/// 默认Endpoint，需要主动从Channel中发送或监听信息
/// </summary>
public class DefaultCore : CommunicationCore<MetadataForDefault>
{
    /// <summary>
    /// 默认Endpoint，需要主动从Channel中发送或监听信息
    /// </summary>
    public DefaultCore(MetadataForDefault metadata) : base(metadata)
    {
    }

    public DefaultCore() : base(new MetadataForDefault())
    {

    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}

/// <summary>
/// 默认Endpoint，需要主动从Channel中发送或监听信息
/// </summary>
/// <typeparam name="TCore"></typeparam>
/// <typeparam name="TMetadata">设置Channel的元数据配置</typeparam>
/// <param name="metadata"></param>
public class DefaultCore<TCore, TMetadata>(TMetadata metadata) : CommunicationCore<TMetadata>(metadata) where TMetadata : CommunicationMetadata<TCore> where TCore : DefaultCore<TCore, TMetadata>
{
    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}