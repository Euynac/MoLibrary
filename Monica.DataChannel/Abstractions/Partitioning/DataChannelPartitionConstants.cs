namespace Monica.DataChannel.Abstractions.Partitioning;

/// <summary>
/// Defines metadata keys used by DataChannel partition-aware transports.
/// </summary>
public static class DataChannelPartitionConstants
{
    /// <summary>
    /// Monica-owned metadata key for the logical partition key.
    /// </summary>
    public const string MonicaPartitionKey = "mo-partition-key";

    /// <summary>
    /// Common metadata key used by brokers and binding components for partition routing.
    /// </summary>
    public const string PartitionKey = "partitionKey";

    /// <summary>
    /// Common metadata key used by Kafka-compatible components for the message key.
    /// </summary>
    public const string MessageKey = "key";
}
