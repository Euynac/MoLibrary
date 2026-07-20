using Monica.DataChannel.Abstractions.Communication;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.Providers.DaprBinding;

public class DaprBindingOptions : CommunicationOptions<DaprBindingEndpoint>
{
    public EDaprBindingType DaprBindingType { get; }

    /// <summary>
    /// Required for MQ bindings. Input bindings must match metadata.route and allow Dapr to determine push targets by inspecting interfaces.
    /// </summary>
    public string? InputListenerRoute { get; set; }

    /// <summary>
    /// Required for MQ bindings using an output binding; should match metadata.name.
    /// </summary>
    public string? OutputBindingName { get; set; }

    /// <summary>
    /// Gets or sets whether input binding messages should be offered to <see cref="Monica.DataChannel.Abstractions.Partitioning.IDaprBindingInputDispatcher"/>.
    /// The default is <see langword="false"/>, which preserves the existing synchronous pipeline behavior.
    /// Enable this only when the dispatcher preserves Dapr ACK semantics by either completing processing before returning
    /// or durably accepting the message before returning success.
    /// </summary>
    public bool EnableInputDispatcher { get; set; }

    /// <summary>
    /// Gets or sets whether output binding calls should include partition metadata when a
    /// <see cref="Monica.DataChannel.Abstractions.Partitioning.IDataChannelPartitionKeyResolver"/> returns a key.
    /// The default is <see langword="true"/>; without a registered resolver no metadata is emitted.
    /// </summary>
    public bool EnableOutputPartitionMetadata { get; set; } = true;

    public override void EnrichOrValidate()
    {
        switch (DaprBindingType)
        {
            case EDaprBindingType.MQTT3:
            case EDaprBindingType.Kafka:
                Type = CommunicationType.MQ;
                if (!Direction.EqualsAny(ConnectionDirection.Output, ConnectionDirection.Input))
                    throw new InvalidOperationException(
                        $"{DaprBindingType} supports only the {ConnectionDirection.Output} or {ConnectionDirection.Input} direction.");
                if (Direction == ConnectionDirection.Input && string.IsNullOrWhiteSpace(InputListenerRoute))
                {
                    throw new InvalidOperationException(
                        $"{nameof(InputListenerRoute)} must be provided when the direction is {ConnectionDirection.Input}.");
                }

                if (Direction == ConnectionDirection.Output && string.IsNullOrWhiteSpace(OutputBindingName))
                {
                    throw new InvalidOperationException(
                        $"{nameof(OutputBindingName)} must be provided when the direction is {ConnectionDirection.Output}.");
                }
                break;
            case EDaprBindingType.Cron:
                Type = CommunicationType.Trigger;
                if (!Direction.EqualsAny(ConnectionDirection.Input))
                    throw new InvalidOperationException(
                        $"{DaprBindingType} supports only the {ConnectionDirection.Input} direction.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(DaprBindingType), DaprBindingType, null);
        }
    }


    public DaprBindingOptions(EDaprBindingType type, ConnectionDirection direction)
    {
        Direction = direction;
        DaprBindingType = type;
    }
}

public enum EDaprBindingType
{
    MQTT3,
    Kafka,
    Cron,
}
