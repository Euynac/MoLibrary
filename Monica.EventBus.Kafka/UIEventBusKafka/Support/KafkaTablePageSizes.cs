namespace Monica.EventBus.Kafka.UIEventBusKafka.Support;

/// <summary>
/// Provides the page-size choices shared by Kafka console evidence tables.
/// </summary>
internal static class KafkaTablePageSizes
{
    /// <summary>
    /// Gets the standard page-size choices used by inventory and detail tables.
    /// </summary>
    public static readonly int[] Standard = [10, 25, 50, 100];

    /// <summary>
    /// Gets the page-size choices centered on the performance table's default row count.
    /// </summary>
    public static readonly int[] Performance = [10, 20, 50, 100];
}
