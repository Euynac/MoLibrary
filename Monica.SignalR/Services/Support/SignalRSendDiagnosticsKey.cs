using Monica.SignalR.Models;

namespace Monica.SignalR.Services.Support;

/// <summary>
/// Stable aggregation key for observed SignalR sends.
/// </summary>
internal readonly record struct SignalRSendDiagnosticsKey(
    string HubName,
    string MethodName,
    SignalRSendTargetKind TargetKind,
    int TargetCount,
    string TargetIdentifierKey);
