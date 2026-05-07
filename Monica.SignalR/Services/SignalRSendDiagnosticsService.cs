using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.SignalR.Models;
using Monica.SignalR.Services.Support;

namespace Monica.SignalR.Services;

/// <summary>
/// Records low-overhead counters for Monica-observed SignalR server-to-client sends.
/// </summary>
public sealed class SignalRSendDiagnosticsService(IOptions<ModuleSignalROption> options)
{
    private readonly ConcurrentDictionary<SignalRSendDiagnosticsKey, SignalRSendMetric> _metrics = new();

    /// <summary>
    /// Gets a value indicating whether SignalR send diagnostics are enabled.
    /// </summary>
    public bool IsEnabled => options.Value.EnableSendDiagnostics;

    /// <summary>
    /// Gets a value indicating whether target identifiers should be retained in diagnostics snapshots.
    /// </summary>
    public bool IncludeTargetIdentifiers => options.Value.IncludeSendDiagnosticTargetIdentifiers;

    /// <summary>
    /// Starts an observed send operation.
    /// </summary>
    internal SignalRSendDiagnosticsOperation StartObservedSend(string hubName, string methodName, SignalRSendTarget target)
    {
        if (!IsEnabled)
        {
            return SignalRSendDiagnosticsOperation.Disabled;
        }

        var key = new SignalRSendDiagnosticsKey(
            hubName,
            methodName,
            target.Kind,
            target.TargetCount,
            BuildTargetIdentifierKey(target.Identifiers));

        var metric = _metrics.GetOrAdd(key, _ => new SignalRSendMetric(key, target.Identifiers));
        return new SignalRSendDiagnosticsOperation(metric, metric.Start());
    }

    /// <summary>
    /// Returns the current diagnostics snapshot.
    /// </summary>
    public SignalRSendDiagnosticsSnapshot GetSnapshot()
    {
        var metrics = _metrics.Values
            .Select(metric => metric.CreateSnapshot())
            .OrderByDescending(metric => metric.PendingSendCount)
            .ThenByDescending(metric => metric.MaxDurationMilliseconds)
            .ThenBy(metric => metric.HubName, StringComparer.Ordinal)
            .ThenBy(metric => metric.MethodName, StringComparer.Ordinal)
            .ToList();

        return new SignalRSendDiagnosticsSnapshot
        {
            IsEnabled = IsEnabled,
            PendingSendCount = metrics.Sum(metric => metric.PendingSendCount),
            TotalStartedCount = metrics.Sum(metric => metric.StartedCount),
            TotalCompletedCount = metrics.Sum(metric => metric.CompletedCount),
            TotalFailedCount = metrics.Sum(metric => metric.FailedCount),
            Metrics = metrics
        };
    }

    private static string BuildTargetIdentifierKey(IReadOnlyList<string> identifiers)
    {
        return identifiers.Count == 0
            ? string.Empty
            : string.Join('\u001f', identifiers.Order(StringComparer.Ordinal));
    }
}

/// <summary>
/// Represents an active observed SignalR send operation.
/// </summary>
internal readonly struct SignalRSendDiagnosticsOperation(SignalRSendMetric? metric, long startTimestamp)
{
    /// <summary>
    /// Gets a disabled operation that does not update counters.
    /// </summary>
    public static SignalRSendDiagnosticsOperation Disabled { get; } = new(null, 0);

    /// <summary>
    /// Completes the operation.
    /// </summary>
    public void Complete(bool failed)
    {
        metric?.Finish(startTimestamp, failed);
    }
}
