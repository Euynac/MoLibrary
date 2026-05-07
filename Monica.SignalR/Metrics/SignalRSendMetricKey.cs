using Monica.SignalR.Models;

namespace Monica.SignalR.Metrics;

/// <summary>
/// Stable aggregation key for Monica-observed SignalR send snapshot state.
/// </summary>
internal readonly record struct SignalRSendMetricKey(
    string HubName,
    string MethodName,
    SignalRSendTargetKind TargetKind,
    int TargetCount,
    string TargetIdentifierKey);
