using Monica.DataChannel.Abstractions.Communication;

namespace Monica.DataChannel.Providers.ActiveMQ;

public class ActiveMqOptions : CommunicationOptions<ActiveMqEndpoint>
{
    public required string BrokerUri { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public string SubscriptionName { get; set; } = nameof(ActiveMqOptions);
    /// <summary>
    /// ClientId for the durable subscriber; ActiveMQ retains messages for this ID while offline and redelivers them when it reconnects. Defaults to a new GUID.
    /// </summary>
    public string ClientId { get; set; } = Guid.NewGuid().ToString();
    public ActiveMqOptions(ConnectionDirection direction = ConnectionDirection.Input)
    {
        Type = CommunicationType.MQ;
        Direction = direction;
    }
}
