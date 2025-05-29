using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.DaprBinding;

public class MetadataForDaprBinding : CommunicationMetadata<DaprBindingCore>
{
    public EDaprBindingType DaprBindingType { get; }

    /// <summary>
    /// 用于MQ类。Dapr Input Binding必填。必须与metadata.route的值一致。Dapr会根据微服务所有的接口判断是否需要推送数据
    /// </summary>
    public string? InputListenerRoute { get; set; }

    /// <summary>
    /// 用于MQ类。Output Binding必填。为metadata.name的值
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