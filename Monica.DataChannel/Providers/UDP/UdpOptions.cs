using Monica.DataChannel.Abstractions.Communication;

namespace Monica.DataChannel.Providers.UDP;

public class UdpOptions : CommunicationOptions<UdpEndpoint>
{
    public required string Address { get; set; }
    public required int Port { get; set; }

    public string? SubscriptionName { get; set; } = nameof(UdpOptions);

    public UdpOptions(ConnectionDirection direction = ConnectionDirection.Input)
    {
        Type = CommunicationType.UDP;
        Direction = direction;
    }
}
