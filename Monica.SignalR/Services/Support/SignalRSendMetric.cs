using System.Diagnostics;
using Monica.SignalR.Models;

namespace Monica.SignalR.Services.Support;

/// <summary>
/// Mutable counter storage for one observed SignalR send metric.
/// </summary>
internal sealed class SignalRSendMetric(
    SignalRSendDiagnosticsKey key,
    IReadOnlyList<string> targetIdentifiers)
{
    private long _pendingSendCount;
    private long _startedCount;
    private long _completedCount;
    private long _failedCount;
    private long _lastDurationTicks;
    private long _maxDurationTicks;
    private long _totalDurationTicks;
    private long _lastStartedAtUtcTicks;
    private long _lastCompletedAtUtcTicks;

    /// <summary>
    /// Marks a send as started and returns its stopwatch timestamp.
    /// </summary>
    public long Start()
    {
        Interlocked.Increment(ref _pendingSendCount);
        Interlocked.Increment(ref _startedCount);
        Interlocked.Exchange(ref _lastStartedAtUtcTicks, DateTime.UtcNow.Ticks);
        return Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Marks a send as completed or failed.
    /// </summary>
    public void Finish(long startTimestamp, bool failed)
    {
        var elapsedTicks = Stopwatch.GetElapsedTime(startTimestamp).Ticks;

        Interlocked.Decrement(ref _pendingSendCount);
        Interlocked.Add(ref _totalDurationTicks, elapsedTicks);
        Interlocked.Exchange(ref _lastDurationTicks, elapsedTicks);
        Interlocked.Exchange(ref _lastCompletedAtUtcTicks, DateTime.UtcNow.Ticks);

        if (failed)
        {
            Interlocked.Increment(ref _failedCount);
        }
        else
        {
            Interlocked.Increment(ref _completedCount);
        }

        UpdateMaxDuration(elapsedTicks);
    }

    /// <summary>
    /// Captures a thread-safe metric snapshot.
    /// </summary>
    public SignalRSendMetricInfo CreateSnapshot()
    {
        var startedCount = Interlocked.Read(ref _startedCount);
        var completedCount = Interlocked.Read(ref _completedCount);
        var failedCount = Interlocked.Read(ref _failedCount);
        var finishedCount = completedCount + failedCount;
        var totalDurationTicks = Interlocked.Read(ref _totalDurationTicks);

        return new SignalRSendMetricInfo
        {
            HubName = key.HubName,
            MethodName = key.MethodName,
            TargetKind = key.TargetKind,
            TargetCount = key.TargetCount,
            TargetIdentifiers = targetIdentifiers.ToList(),
            PendingSendCount = Interlocked.Read(ref _pendingSendCount),
            StartedCount = startedCount,
            CompletedCount = completedCount,
            FailedCount = failedCount,
            LastDurationMilliseconds = ToMilliseconds(Interlocked.Read(ref _lastDurationTicks)),
            MaxDurationMilliseconds = ToMilliseconds(Interlocked.Read(ref _maxDurationTicks)),
            AverageDurationMilliseconds = finishedCount == 0 ? 0 : ToMilliseconds(totalDurationTicks / finishedCount),
            LastStartedAtUtc = ToUtcDateTime(Interlocked.Read(ref _lastStartedAtUtcTicks)),
            LastCompletedAtUtc = ToUtcDateTime(Interlocked.Read(ref _lastCompletedAtUtcTicks))
        };
    }

    private void UpdateMaxDuration(long elapsedTicks)
    {
        while (true)
        {
            var current = Interlocked.Read(ref _maxDurationTicks);
            if (elapsedTicks <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maxDurationTicks, elapsedTicks, current) == current)
            {
                return;
            }
        }
    }

    private static double ToMilliseconds(long ticks)
    {
        return TimeSpan.FromTicks(ticks).TotalMilliseconds;
    }

    private static DateTime? ToUtcDateTime(long ticks)
    {
        return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }
}
