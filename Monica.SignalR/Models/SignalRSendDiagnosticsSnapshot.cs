namespace Monica.SignalR.Models;

/// <summary>
/// Snapshot of Monica-observed SignalR server-to-client send activity.
/// </summary>
public sealed class SignalRSendDiagnosticsSnapshot
{
    /// <summary>
    /// Gets or sets a value indicating whether send diagnostics are enabled for the module.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets or sets the UTC time when this snapshot was produced.
    /// </summary>
    public DateTime SnapshotTimeUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets a note that explains what the metric can and cannot represent.
    /// </summary>
    public string Description { get; init; } =
        "These counters measure Monica-observed pending SignalR send tasks, not ASP.NET Core SignalR private transport queue depth.";

    /// <summary>
    /// Gets or sets the total number of currently in-flight observed send tasks.
    /// </summary>
    public long PendingSendCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of observed send tasks started since process start.
    /// </summary>
    public long TotalStartedCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of observed send tasks completed successfully since process start.
    /// </summary>
    public long TotalCompletedCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of observed send tasks that failed since process start.
    /// </summary>
    public long TotalFailedCount { get; init; }

    /// <summary>
    /// Gets or sets the observed method and target metrics.
    /// </summary>
    public List<SignalRSendMetricInfo> Metrics { get; init; } = [];
}

/// <summary>
/// Aggregated send diagnostics for a hub method and target selection.
/// </summary>
public sealed class SignalRSendMetricInfo
{
    /// <summary>
    /// Gets or sets the hub type name that emitted the send.
    /// </summary>
    public required string HubName { get; init; }

    /// <summary>
    /// Gets or sets the strongly typed client contract method name.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Gets or sets the kind of client target used for the send.
    /// </summary>
    public SignalRSendTargetKind TargetKind { get; init; }

    /// <summary>
    /// Gets or sets the number of explicit target identifiers supplied for this target selection.
    /// </summary>
    public int TargetCount { get; init; }

    /// <summary>
    /// Gets or sets the explicit target identifiers when target identifier capture is enabled.
    /// </summary>
    public List<string> TargetIdentifiers { get; init; } = [];

    /// <summary>
    /// Gets or sets the current number of in-flight send tasks for this metric.
    /// </summary>
    public long PendingSendCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of observed send tasks started for this metric.
    /// </summary>
    public long StartedCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of observed send tasks completed successfully for this metric.
    /// </summary>
    public long CompletedCount { get; init; }

    /// <summary>
    /// Gets or sets the total number of observed send tasks that failed for this metric.
    /// </summary>
    public long FailedCount { get; init; }

    /// <summary>
    /// Gets or sets the last observed send duration in milliseconds.
    /// </summary>
    public double LastDurationMilliseconds { get; init; }

    /// <summary>
    /// Gets or sets the maximum observed send duration in milliseconds.
    /// </summary>
    public double MaxDurationMilliseconds { get; init; }

    /// <summary>
    /// Gets or sets the average observed send duration in milliseconds.
    /// </summary>
    public double AverageDurationMilliseconds { get; init; }

    /// <summary>
    /// Gets or sets the UTC time when a send was last started for this metric.
    /// </summary>
    public DateTime? LastStartedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the UTC time when a send last completed or failed for this metric.
    /// </summary>
    public DateTime? LastCompletedAtUtc { get; init; }
}
