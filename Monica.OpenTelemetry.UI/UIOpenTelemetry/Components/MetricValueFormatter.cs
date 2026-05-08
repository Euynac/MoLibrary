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
            [UNIT_SECONDS] = new("Seconds", "seconds"),
            [UNIT_BYTES] = new("Bytes", "bytes"),
            [UNIT_BYTES_PER_SECOND] = new("BytesPerSecond", "bytes/second"),
            [UNIT_PERCENT] = new("Percent", "percent"),
            ["assembly"] = new("Assembly", "assemblies"),
            ["challenge"] = new("AuthenticationChallenge", "authentication challenges"),
            ["circuit"] = new("Circuit", "circuits"),
            ["collection"] = new("Collection", "collections"),
            ["collections"] = new("Collections", "collections"),
            ["connection"] = new("Connection", "connections"),
            ["contention"] = new("Contention", "contentions"),
            ["cpu"] = new("Cpu", "CPUs"),
            ["elements"] = new("Elements", "elements"),
            ["errors"] = new("Errors", "errors"),
            ["exception"] = new("Exception", "exceptions"),
            ["forbid"] = new("AuthenticationForbid", "forbid actions"),
            ["handshake"] = new("Handshake", "handshakes"),
            ["match_attempt"] = new("RoutingMatchAttempt", "routing match attempts"),
            ["method"] = new("Method", "methods"),
            ["request"] = new("Request", "requests"),
            ["route"] = new("Route", "routes"),
            ["services"] = new("Services", "services"),
            ["sign_in"] = new("SignIn", "sign-ins"),
            ["sign_out"] = new("SignOut", "sign-outs"),
            ["thread"] = new("Thread", "threads"),
            ["threads"] = new("Threads", "threads"),
            ["timer"] = new("Timer", "timers"),
            ["transitions"] = new("Transitions", "transitions"),
            ["work_item"] = new("WorkItem", "work items")
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

        var localized = localizer[$"UnitDisplay:{resourceKey}"];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }

    private sealed record UnitLabel(string ResourceKey, string Fallback);
}
