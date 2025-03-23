using BuildingBlocksPlatform.DataChannel.CoreCommunication;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.UDP;

public class MetadataForUDP : CommunicationMetadata<UDPCore>
{
    public required string Address { get; set; }
    public required int Port { get; set; }

    public string? SubscriptionName { get; set; } = nameof(MetadataForUDP);

    public MetadataForUDP(EConnectionDirection direction = EConnectionDirection.Input)
    {
        Type = ECommunicationType.UDP;
        Direction = direction;
    }
}
