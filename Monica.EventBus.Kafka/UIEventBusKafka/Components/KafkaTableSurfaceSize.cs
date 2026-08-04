namespace Monica.EventBus.Kafka.UIEventBusKafka.Components;

/// <summary>
/// Defines the minimum evidence width owned by a Kafka console table surface.
/// </summary>
public enum KafkaTableSurfaceSize
{
    /// <summary>
    /// Supports compact four-column evidence tables.
    /// </summary>
    Compact,

    /// <summary>
    /// Supports standard inventory tables with actions.
    /// </summary>
    Standard,

    /// <summary>
    /// Supports detailed operational evidence tables.
    /// </summary>
    Wide,

    /// <summary>
    /// Supports the widest performance evidence tables.
    /// </summary>
    ExtraWide
}
