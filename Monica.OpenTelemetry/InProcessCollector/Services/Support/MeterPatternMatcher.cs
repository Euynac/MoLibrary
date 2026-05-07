namespace Monica.OpenTelemetry.InProcessCollector.Services.Support;

/// <summary>
/// Matches meter names against exact or wildcard patterns used by Monica.OpenTelemetry.
/// </summary>
internal static class MeterPatternMatcher
{
    public static bool IsMatch(string meterName, IReadOnlyCollection<string> patterns)
    {
        return patterns.Count == 0 || patterns.Any(pattern => IsMatch(meterName, pattern));
    }

    private static bool IsMatch(string meterName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        if (pattern == "*")
        {
            return true;
        }

        var starIndex = pattern.IndexOf('*', StringComparison.Ordinal);
        if (starIndex < 0)
        {
            return string.Equals(meterName, pattern, StringComparison.Ordinal);
        }

        var prefix = pattern[..starIndex];
        var suffix = pattern[(starIndex + 1)..];
        return meterName.StartsWith(prefix, StringComparison.Ordinal) &&
               meterName.EndsWith(suffix, StringComparison.Ordinal);
    }
}
