using System.Globalization;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.Components;

/// <summary>
/// Formats metric values for dashboard presentation without changing the exported raw values.
/// </summary>
internal static class MetricValueFormatter
{
    private const double KIB = 1024d;
    private const double MIB = KIB * 1024d;
    private const double GIB = MIB * 1024d;
    private const double TIB = GIB * 1024d;

    public static string Format(double value, string? unit)
    {
        return NormalizeUnit(unit) switch
        {
            "By" => FormatBytes(value),
            "By/s" => FormatBytesPerSecond(value),
            "%" => value.ToString("F2", CultureInfo.InvariantCulture) + "%",
            _ => value.ToString("G4", CultureInfo.InvariantCulture)
        };
    }

    private static string FormatBytes(double bytes)
    {
        var absolute = Math.Abs(bytes);
        var (scale, suffix) = absolute switch
        {
            >= TIB => (TIB, "TiB"),
            >= GIB => (GIB, "GiB"),
            >= MIB => (MIB, "MiB"),
            >= KIB => (KIB, "KiB"),
            _ => (1d, "B")
        };

        return (bytes / scale).ToString("G4", CultureInfo.InvariantCulture) + " " + suffix;
    }

    private static string FormatBytesPerSecond(double bytesPerSecond)
    {
        return FormatBytes(bytesPerSecond) + "/s";
    }

    private static string? NormalizeUnit(string? unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }
}
