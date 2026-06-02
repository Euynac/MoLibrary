using System.Globalization;
using System.Text.Json;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationScalarTextCodec
{
    private const string TIME_SPAN_STORAGE_FORMAT = "c";
    private const string TIME_SPAN_SHORT_DISPLAY_FORMAT = @"hh\:mm";
    private const string TIME_SPAN_LONG_DISPLAY_FORMAT = @"hh\:mm\:ss";

    public static bool TryParseTimeSpan(string? text, out TimeSpan value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        return TimeSpan.TryParseExact(
                   text,
                   [TIME_SPAN_STORAGE_FORMAT, "g", "G", @"d\.hh\:mm\:ss", TIME_SPAN_LONG_DISPLAY_FORMAT, TIME_SPAN_SHORT_DISPLAY_FORMAT],
                   CultureInfo.InvariantCulture,
                   out value)
               || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out value);
    }

    public static string FormatTimeSpan(TimeSpan value)
    {
        if (value >= TimeSpan.Zero && value.Days == 0 && value.Ticks % TimeSpan.TicksPerSecond == 0)
        {
            return value.Seconds == 0
                ? value.ToString(TIME_SPAN_SHORT_DISPLAY_FORMAT, CultureInfo.InvariantCulture)
                : value.ToString(TIME_SPAN_LONG_DISPLAY_FORMAT, CultureInfo.InvariantCulture);
        }

        return value.ToString(TIME_SPAN_STORAGE_FORMAT, CultureInfo.InvariantCulture);
    }

    public static string NormalizeTimeSpanDisplay(string displayValue)
    {
        return TryParseTimeSpan(displayValue, out var value)
            ? FormatTimeSpan(value)
            : displayValue;
    }

    public static string ToTimeSpanJson(TimeSpan value)
    {
        return JsonSerializer.Serialize(value.ToString(TIME_SPAN_STORAGE_FORMAT, CultureInfo.InvariantCulture));
    }

    public static bool TimeSpanEquals(string? left, string? right)
    {
        return TryParseTimeSpan(left, out var leftValue)
               && TryParseTimeSpan(right, out var rightValue)
               && leftValue == rightValue;
    }
}
