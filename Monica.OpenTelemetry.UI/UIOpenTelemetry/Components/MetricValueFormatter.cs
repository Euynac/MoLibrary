using System.Globalization;
using Microsoft.Extensions.Localization;
using Monica.OpenTelemetry.UI.Localization;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.Components;

/// <summary>
/// Formats metric values for dashboard presentation without changing the exported raw values.
/// </summary>
internal static class MetricValueFormatter
{
    private const string UNIT_SECONDS = "s";
    private const string UNIT_BYTES = "By";
    private const string UNIT_BYTES_PER_SECOND = "By/s";
    private const string UNIT_PERCENT = "%";
    private const double KIB = 1024d;
    private const double MIB = KIB * 1024d;
    private const double GIB = MIB * 1024d;
    private const double TIB = GIB * 1024d;

    private static readonly IReadOnlyDictionary<string, UnitLabel> UnitLabels =
        new Dictionary<string, UnitLabel>(StringComparer.Ordinal)
        {
            [UNIT_SECONDS] = new("UnitDisplay:Seconds", "seconds"),
            [UNIT_BYTES] = new("UnitDisplay:Bytes", "bytes"),
            [UNIT_BYTES_PER_SECOND] = new("UnitDisplay:BytesPerSecond", "bytes/second"),
            [UNIT_PERCENT] = new("UnitDisplay:Percent", "percent"),
            ["assembly"] = new("UnitDisplay:Assembly", "assemblies"),
            ["challenge"] = new("UnitDisplay:AuthenticationChallenge", "authentication challenges"),
            ["circuit"] = new("UnitDisplay:Circuit", "circuits"),
            ["collection"] = new("UnitDisplay:Collection", "collections"),
            ["collections"] = new("UnitDisplay:Collections", "collections"),
            ["connection"] = new("UnitDisplay:Connection", "connections"),
            ["contention"] = new("UnitDisplay:Contention", "contentions"),
            ["cpu"] = new("UnitDisplay:Cpu", "CPUs"),
            ["elements"] = new("UnitDisplay:Elements", "elements"),
            ["errors"] = new("UnitDisplay:Errors", "errors"),
            ["exception"] = new("UnitDisplay:Exception", "exceptions"),
            ["forbid"] = new("UnitDisplay:AuthenticationForbid", "forbid actions"),
            ["handshake"] = new("UnitDisplay:Handshake", "handshakes"),
            ["match_attempt"] = new("UnitDisplay:RoutingMatchAttempt", "routing match attempts"),
            ["method"] = new("UnitDisplay:Method", "methods"),
            ["request"] = new("UnitDisplay:Request", "requests"),
            ["route"] = new("UnitDisplay:Route", "routes"),
            ["services"] = new("UnitDisplay:Services", "services"),
            ["sign_in"] = new("UnitDisplay:SignIn", "sign-ins"),
            ["sign_out"] = new("UnitDisplay:SignOut", "sign-outs"),
            ["thread"] = new("UnitDisplay:Thread", "threads"),
            ["threads"] = new("UnitDisplay:Threads", "threads"),
            ["timer"] = new("UnitDisplay:Timer", "timers"),
            ["transitions"] = new("UnitDisplay:Transitions", "transitions"),
            ["work_item"] = new("UnitDisplay:WorkItem", "work items")
        };

    public static string Format(
        double value,
        string? unit,
        IStringLocalizer<OpenTelemetryUIResource>? localizer = null)
    {
        return NormalizeUnit(unit) switch
        {
            UNIT_BYTES => FormatBytes(value),
            UNIT_BYTES_PER_SECOND => FormatBytesPerSecond(value),
            UNIT_PERCENT => FormatNumber(value) + "%",
            UNIT_SECONDS => FormatSeconds(value),
            { } normalizedUnit => FormatNumber(value) + " " + FormatUnitLabel(normalizedUnit, localizer),
            _ => FormatNumber(value)
        };
    }

    public static string FormatUnitLabel(
        string? unit,
        IStringLocalizer<OpenTelemetryUIResource>? localizer = null)
    {
        var normalizedUnit = NormalizeUnit(unit);
        if (normalizedUnit is null)
        {
            return string.Empty;
        }

        var unitKey = GetUnitKey(normalizedUnit);
        return UnitLabels.TryGetValue(unitKey, out var label)
            ? Localize(localizer, label.ResourceKey, label.Fallback)
            : unitKey.Replace('_', ' ');
    }

    public static bool IsCountUnit(string? unit)
    {
        return NormalizeUnit(unit) is { } normalizedUnit &&
               normalizedUnit is not UNIT_SECONDS and not UNIT_BYTES and not UNIT_BYTES_PER_SECOND and not UNIT_PERCENT;
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

        return FormatNumber(bytes / scale) + " " + suffix;
    }

    private static string FormatBytesPerSecond(double bytesPerSecond)
    {
        return FormatBytes(bytesPerSecond) + "/s";
    }

    private static string FormatSeconds(double seconds)
    {
        var absolute = Math.Abs(seconds);
        if (absolute == 0)
        {
            return "0 s";
        }

        if (absolute < 0.001d)
        {
            return FormatNumber(seconds * 1_000_000d) + " us";
        }

        if (absolute < 1d)
        {
            return FormatNumber(seconds * 1_000d) + " ms";
        }

        return FormatNumber(seconds) + " s";
    }

    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var absolute = Math.Abs(value);
        if (absolute == 0)
        {
            return "0";
        }

        if (absolute is >= 1_000_000_000d or < 0.001d)
        {
            return value.ToString("0.###E+0", CultureInfo.InvariantCulture);
        }

        return absolute switch
        {
            >= 1000d => value.ToString("#,0.###", CultureInfo.InvariantCulture),
            >= 100d => value.ToString("0.#", CultureInfo.InvariantCulture),
            >= 10d => value.ToString("0.##", CultureInfo.InvariantCulture),
            _ => value.ToString("0.###", CultureInfo.InvariantCulture)
        };
    }

    private static string? NormalizeUnit(string? unit)
    {
        return string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }

    private static string GetUnitKey(string unit)
    {
        return unit is ['{', .., '}'] ? unit[1..^1] : unit;
    }

    private static string Localize(
        IStringLocalizer<OpenTelemetryUIResource>? localizer,
        string resourceKey,
        string fallback)
    {
        if (localizer is null)
        {
            return fallback;
        }

        var localized = localizer[resourceKey];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }

    private sealed record UnitLabel(string ResourceKey, string Fallback);
}
