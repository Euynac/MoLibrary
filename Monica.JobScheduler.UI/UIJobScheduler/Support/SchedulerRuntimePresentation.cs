using Microsoft.Extensions.Localization;
using Monica.JobScheduler.UI.Localization;

namespace Monica.JobScheduler.UI.UIJobScheduler.Support;

/// <summary>
/// Formats scheduler runtime values — configuration intervals and heartbeat ages — with localized unit
/// abbreviations instead of raw <see cref="TimeSpan"/> strings.
/// </summary>
internal static class SchedulerRuntimePresentation
{
    /// <summary>
    /// Formats one interval with the largest natural unit from days down to milliseconds.
    /// </summary>
    internal static string FormatInterval(TimeSpan value, IStringLocalizer<JobSchedulerResource> localizer)
    {
        if (value >= TimeSpan.FromDays(1))
        {
            return Format(localizer["Runtime:Format:Days"], value.TotalDays);
        }

        if (value >= TimeSpan.FromHours(1))
        {
            return Format(localizer["Runtime:Format:Hours"], value.TotalHours);
        }

        if (value >= TimeSpan.FromMinutes(1))
        {
            return Format(localizer["Runtime:Format:Minutes"], value.TotalMinutes);
        }

        if (value >= TimeSpan.FromSeconds(1))
        {
            return Format(localizer["Runtime:Format:Seconds"], value.TotalSeconds);
        }

        return Format(localizer["Runtime:Format:Milliseconds"], value.TotalMilliseconds);
    }

    /// <summary>
    /// Formats the heartbeat age of one worker instance relative to the capture instant.
    /// </summary>
    internal static string FormatAge(
        DateTimeOffset capturedAtUtc,
        DateTimeOffset observedAtUtc,
        IStringLocalizer<JobSchedulerResource> localizer)
    {
        var age = capturedAtUtc - observedAtUtc;
        return age < TimeSpan.Zero
            ? FormatInterval(TimeSpan.Zero, localizer)
            : FormatInterval(age, localizer);
    }

    private static string Format(string pattern, double value) =>
        string.Format(pattern, value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture));
}
