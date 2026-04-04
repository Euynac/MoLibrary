using Confluent.Kafka;
using Monica.DataChannel.Abstractions.Communication;

namespace Monica.DataChannel.Providers.Kafka;

public class KafkaOptions : CommunicationOptions<KafkaEndpoint>
{
    /// <summary>
    /// Kafka broker addresses (e.g., "localhost:9092")
    /// </summary>
    public required string BootstrapServers { get; set; }
    
    /// <summary>
    /// Topic name for publishing/subscribing
    /// </summary>
    public string? Topic { get; set; }
    
    /// <summary>
    /// Consumer group ID for consumer instances
    /// </summary>
    public string ConsumerGroupId { get; set; } = nameof(KafkaOptions);
    
    /// <summary>
    /// Client identifier
    /// </summary>
    public string ClientId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Security protocol for authentication
    /// </summary>
    public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.SaslPlaintext;

    /// <summary>
    /// SaslMechanism
    /// </summary>
    public SaslMechanism SaslMechanism { get; set; } = SaslMechanism.ScramSha512;
    
    /// <summary>
    /// Username for SASL authentication (optional)
    /// </summary>
    public string? Username { get; set; }
    
    /// <summary>
    /// Password for SASL authentication (optional)
    /// </summary>
    public string? Password { get; set; }
    
    /// <summary>
    /// Consumer offset behavior when no initial offset exists
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Latest;
    
    /// <summary>
    /// Auto-commit configuration for consumer
    /// </summary>
    public bool EnableAutoCommit { get; set; } = true;
    
    /// <summary>
    /// Auto-commit interval in milliseconds
    /// </summary>
    public int AutoCommitIntervalMs { get; set; } = 5000;

    public KafkaOptions(ConnectionDirection direction = ConnectionDirection.Input)
    {
        Type = CommunicationType.MQ;
        Direction = direction;
    }
    
    public override void EnrichOrValidate()
    {
        if (string.IsNullOrEmpty(BootstrapServers))
            throw new ArgumentException("BootstrapServers is required for Kafka connection");
            
        if (string.IsNullOrEmpty(Topic) && Direction != ConnectionDirection.None)
            throw new ArgumentException("Topic is required when Direction is set");
    }
}