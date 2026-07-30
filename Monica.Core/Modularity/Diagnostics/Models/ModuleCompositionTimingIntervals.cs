namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>Represents one monotonic half-open interval on the composition timeline.</summary>
internal readonly record struct ModuleCompositionTimingInterval(double StartMs, double EndMs);

/// <summary>Normalizes and measures potentially overlapping composition intervals.</summary>
internal static class ModuleCompositionTimingIntervals
{
    /// <summary>Returns a finite, non-negative diagnostic duration.</summary>
    internal static double NormalizeDuration(double durationMs)
    {
        return double.IsFinite(durationMs) ? Math.Max(0, durationMs) : 0;
    }

    /// <summary>
    /// Clips intervals to the supplied range and measures their union so overlapping observations count once.
    /// </summary>
    internal static double MeasureUnion(
        IEnumerable<ModuleCompositionTimingInterval> intervals,
        double rangeStartMs,
        double rangeEndMs)
    {
        if (!double.IsFinite(rangeStartMs)
            || !double.IsFinite(rangeEndMs)
            || rangeEndMs <= rangeStartMs)
        {
            return 0;
        }

        var normalized = intervals
            .Where(static interval => double.IsFinite(interval.StartMs) && double.IsFinite(interval.EndMs))
            .Select(interval => new ModuleCompositionTimingInterval(
                Math.Clamp(interval.StartMs, rangeStartMs, rangeEndMs),
                Math.Clamp(interval.EndMs, rangeStartMs, rangeEndMs)))
            .Where(static interval => interval.EndMs > interval.StartMs)
            .OrderBy(static interval => interval.StartMs)
            .ThenBy(static interval => interval.EndMs)
            .ToArray();
        if (normalized.Length == 0)
        {
            return 0;
        }

        var totalDurationMs = 0d;
        var currentStartMs = normalized[0].StartMs;
        var currentEndMs = normalized[0].EndMs;
        foreach (var interval in normalized.AsSpan(1))
        {
            if (interval.StartMs <= currentEndMs)
            {
                currentEndMs = Math.Max(currentEndMs, interval.EndMs);
                continue;
            }

            totalDurationMs += currentEndMs - currentStartMs;
            currentStartMs = interval.StartMs;
            currentEndMs = interval.EndMs;
        }

        return totalDurationMs + currentEndMs - currentStartMs;
    }
}
