namespace Monica.DataChannel.Metrics;

/// <summary>
/// Defines meter and instrument names emitted by the DataChannel module.
/// </summary>
public static class DataChannelMetricNames
{
    /// <summary>
    /// Meter name used for DataChannel metrics.
    /// </summary>
    public const string MeterName = "Monica.DataChannel";

    /// <summary>
    /// Counter instrument that records messages observed by DataChannel middleware.
    /// </summary>
    public const string Messages = "monica.datachannel.messages";
}
