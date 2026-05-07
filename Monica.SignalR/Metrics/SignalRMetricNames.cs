namespace Monica.SignalR.Metrics;

/// <summary>
/// Defines meter and instrument names emitted by the SignalR module.
/// </summary>
public static class SignalRMetricNames
{
    /// <summary>
    /// Meter name used for SignalR hub metrics.
    /// </summary>
    public const string MeterName = "Monica.SignalR.Hub";

    /// <summary>
    /// Counter instrument that records Monica-observed SignalR server-to-client send lifecycle events.
    /// </summary>
    public const string HubSendEvents = "monica.signalr.hub.send.events";

    /// <summary>
    /// Observable gauge instrument that reports current pending Monica-observed SignalR sends.
    /// </summary>
    public const string HubSendPending = "monica.signalr.hub.send.pending";

    /// <summary>
    /// Histogram instrument that records Monica-observed SignalR server-to-client send duration in seconds.
    /// </summary>
    public const string HubSendDuration = "monica.signalr.hub.send.duration";
}
