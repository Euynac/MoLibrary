using MoLibrary.DataChannel.CoreCommunication;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.ActiveMQ;

public class MetadataForActiveMQ : CommunicationMetadata<ActiveMQCore>
{
    public required string BrokerUri { get; set; }
    public string TopicName { get; set; }
    public string QueueName { get; set; }
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    
    public string? SubscriptionName { get; set; } = nameof(MetadataForActiveMQ);
    /// <summary>
    /// ClientId的持久化订阅者，ActiveMQ会给这个指定ClientId的持久化订阅者保存它断线期间接收到的消息，当下次这个ClientId的订阅者重新连接时，ActiveMQ会将断线期间接收到的消息发给订阅者。默认使用Guid生成
    /// </summary>
    public string ClientId { get; set; } = Guid.NewGuid().ToString();
    public MetadataForActiveMQ(EConnectionDirection direction = EConnectionDirection.Input)
    {
        Type = ECommunicationType.MQ;
        Direction = direction;
    }
}
