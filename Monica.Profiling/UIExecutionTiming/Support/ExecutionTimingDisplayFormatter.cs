using Monica.Profiling.ExecutionTiming.Models;
using Monica.Tool.Text;

namespace Monica.Profiling.UIExecutionTiming.Support;

internal static class ExecutionTimingDisplayFormatter
{
    public static string FormatDateTime(DateTimeOffset? timestamp)
    {
        return timestamp?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
    }

    public static string FormatDuration(double milliseconds)
    {
        return $"{milliseconds:0.##}ms";
    }

    public static string FormatMemory(long? bytes)
    {
        return bytes.HasValue ? bytes.Value.FormatByteSize() : "—";
    }

    public static string FormatRatePerMinute(ExecutionTimingStatistics statistics)
    {
        var elapsedMinutes = Math.Max((DateTimeOffset.UtcNow - statistics.FirstRecordedAt).TotalMinutes, 1d);
        return $"{statistics.ExecutionCount / elapsedMinutes:0.##}";
    }

    public static string FormatRunningDuration(RunningExecutionTimingInfo operation)
    {
        return $"{(DateTimeOffset.UtcNow - operation.StartedAt).TotalSeconds:0.##}s";
    }
}
