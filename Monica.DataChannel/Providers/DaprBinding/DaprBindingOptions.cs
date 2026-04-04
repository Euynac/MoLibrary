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

    public override void EnrichOrValidate()
    {
        switch (DaprBindingType)
        {
            case EDaprBindingType.MQTT3:
            case EDaprBindingType.Kafka:
                Type = CommunicationType.MQ;
                if (!Direction.EqualsAny(ConnectionDirection.Output, ConnectionDirection.Input))
                    throw new InvalidOperationException(
                        $"{DaprBindingType}仅支持{ConnectionDirection.Output}或{ConnectionDirection.Input}");
                if (Direction == ConnectionDirection.Input && string.IsNullOrWhiteSpace(InputListenerRoute))
                {
                    throw new InvalidOperationException(
                        $"{nameof(InputListenerRoute)}在{ConnectionDirection.Input}时必须有值");
                }

                if (Direction == ConnectionDirection.Output && string.IsNullOrWhiteSpace(OutputBindingName))
                {
                    throw new InvalidOperationException(
                        $"{nameof(OutputBindingName)}在{ConnectionDirection.Output}时必须有值");
                }
                break;
            case EDaprBindingType.Cron:
                Type = CommunicationType.Trigger;
                if (!Direction.EqualsAny(ConnectionDirection.Input))
                    throw new InvalidOperationException(
                        $"{DaprBindingType}仅支持{ConnectionDirection.Input}");
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
