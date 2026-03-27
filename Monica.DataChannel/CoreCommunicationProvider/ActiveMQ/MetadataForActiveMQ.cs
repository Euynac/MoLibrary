using Monica.DataChannel.CoreCommunication;

namespace Monica.DataChannel.CoreCommunicationProvider.ActiveMQ;

public class MetadataForActiveMQ : CommunicationMetadata<ActiveMQCore>
{
    public required string BrokerUri { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public string SubscriptionName { get; set; } = nameof(MetadataForActiveMQ);
    /// <summary>
    /// ClientId for the durable subscriber; ActiveMQ retains messages for this ID while offline and redelivers them when it reconnects. Defaults to a new GUID.
    /// </summary>
    public string ClientId { get; set; } = Guid.NewGuid().ToString();
    public MetadataForActiveMQ(EConnectionDirection direction = EConnectionDirection.Input)
    {
        Type = ECommunicationType.MQ;
        Direction = direction;
    }
}
