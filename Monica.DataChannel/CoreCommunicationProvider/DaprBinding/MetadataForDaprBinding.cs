using Monica.DataChannel.CoreCommunication;
using Monica.Tool.Extensions;

namespace Monica.DataChannel.CoreCommunicationProvider.DaprBinding;

public class MetadataForDaprBinding : CommunicationMetadata<DaprBindingCore>
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

    public override void EnrichOrValidate()
    {
        switch (DaprBindingType)
        {
            case EDaprBindingType.MQTT3:
            case EDaprBindingType.Kafka:
                Type = ECommunicationType.MQ;
                if (!Direction.EqualsAny(EConnectionDirection.Output, EConnectionDirection.Input))
                    throw new InvalidOperationException(
                        $"{DaprBindingType}仅支持{EConnectionDirection.Output}或{EConnectionDirection.Input}");
                if (Direction == EConnectionDirection.Input && string.IsNullOrWhiteSpace(InputListenerRoute))
                {
                    throw new InvalidOperationException(
                        $"{nameof(InputListenerRoute)}在{EConnectionDirection.Input}时必须有值");
                }

                if (Direction == EConnectionDirection.Output && string.IsNullOrWhiteSpace(OutputBindingName))
                {
                    throw new InvalidOperationException(
                        $"{nameof(OutputBindingName)}在{EConnectionDirection.Output}时必须有值");
                }
                break;
            case EDaprBindingType.Cron:
                Type = ECommunicationType.Trigger;
                if (!Direction.EqualsAny(EConnectionDirection.Input))
                    throw new InvalidOperationException(
                        $"{DaprBindingType}仅支持{EConnectionDirection.Input}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(DaprBindingType), DaprBindingType, null);
        }
    }


    public MetadataForDaprBinding(EDaprBindingType type, EConnectionDirection direction)
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
