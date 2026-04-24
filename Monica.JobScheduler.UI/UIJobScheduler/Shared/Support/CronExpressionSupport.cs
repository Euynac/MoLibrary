using Cronos;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Monica.Core.Results;
using Monica.JobScheduler.UI.Localization;
using Monica.Modules;

namespace Monica.JobScheduler.UI.UIJobScheduler.Shared.Support;

/// <summary>
/// Cron expression format type
/// </summary>
public enum CronFormat
{
    /// <summary>
    /// Standard format (5 paragraphs): minute hour day month week
    /// </summary>
    Standard,

    /// <summary>
    /// Quartz format (6 segments): seconds, minutes, hours, days, months, weeks
    /// </summary>
    Quartz
}

/// <summary>
/// Cron expression service, providing expression parsing, validation and execution time calculation
/// Note: The parsing description function requires the component to provide a JS module reference
/// </summary>
public class CronExpressionSupport(
    IStringLocalizer<JobSchedulerResource> localizer,
    IOptions<ModuleClockOption> clockOptions)
{
    private readonly TimeZoneInfo _cronTimeZone = clockOptions.Value.ConfiguredTimeZone ?? TimeZoneInfo.Local;

    /// <summary>
    /// Verify that Cron expression is valid
    /// </summary>
    public Res<bool> ValidateExpression(string expression, CronFormat format)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Res.Fail(localizer["Services:Errors:ExpressionEmpty"].Value);
        }

        try
        {
            var cronFormat = format == CronFormat.Quartz
                ? Cronos.CronFormat.IncludeSeconds
                : Cronos.CronFormat.Standard;

            CronExpression.Parse(expression, cronFormat);
            return true; // Implicitly converted to Res<bool>(true).
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:ExpressionFormatError", ex.Message].Value);
        }
    }

    /// <summary>
    /// Get the next N execution times of an expression
    /// </summary>
    public Res<List<DateTime>> GetNextOccurrences(string expression, CronFormat format, int count = 5, DateTime? fromTime = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Res.Fail(localizer["Services:Errors:ExpressionEmpty"].Value);
        }

        if (count <= 0 || count > 100)
        {
            return Res.Fail(localizer["Services:Errors:CountRange"].Value);
        }

        try
        {
            var cronFormat = format == CronFormat.Quartz
                ? Cronos.CronFormat.IncludeSeconds
                : Cronos.CronFormat.Standard;

            var cronExpression = CronExpression.Parse(expression, cronFormat);
            var results = new List<DateTime>();
            var currentTime = fromTime ?? DateTime.UtcNow;

            for (int i = 0; i < count; i++)
            {
                var next = cronExpression.GetNextOccurrence(currentTime, _cronTimeZone);
                if (next == null)
                {
                    break;
                }

                results.Add(next.Value);
                currentTime = next.Value;
            }

            return results; // Implicitly converted to Res<List<DateTime>>(results).
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:CalculateOccurrencesFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Parse expressions into readable localized descriptions using the JavaScript cronstrue library.
    /// </summary>
    /// <param name="jsModule">JS module reference provided by the component</param>
    /// <param name="expression">Cron expression</param>
    /// <param name="format">Expression format</param>
    public async Task<Res<string>> ParseToDescriptionAsync(IJSObjectReference jsModule, string expression, CronFormat format)
    {
        if (jsModule == null)
        {
            return Res.Fail(localizer["Services:Errors:JsModuleNotLoaded"]);
        }

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Res.Fail(localizer["Services:Errors:ExpressionEmpty"]);
        }

        try
        {
            var result = await jsModule.InvokeAsync<CronParseResult>(
                "parseCronExpression",
                expression,
                format == CronFormat.Quartz ? "quartz" : "standard",
                CultureInfo.CurrentUICulture.Name);

            return result.Success
                ? Res.Ok<string>(result.Description ?? "")
                : Res.Fail(result.Error ?? localizer["Services:Errors:ParseFailed"]);
        }
        catch (JSException jsEx)
        {
            return Res.Fail(localizer["Services:Errors:JsInvokeFailed", jsEx.Message]);
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:ParseFailedWithMessage", ex.Message]);
        }
    }

    /// <summary>
    /// Build cron expressions from easy setup
    /// </summary>
    public Res<string> BuildFromSimpleSettings(SimpleSettings settings, CronFormat format)
    {
        try
        {
            var expression = settings.Type switch
            {
                SimpleSettingsType.EveryMinute => format == CronFormat.Quartz ? "0 * * * * ?" : "* * * * *",
                SimpleSettingsType.EveryHour => format == CronFormat.Quartz ? "0 0 * * * ?" : "0 * * * *",
                SimpleSettingsType.EveryDay => format == CronFormat.Quartz
                    ? $"0 {settings.Minute} {settings.Hour} * * ?"
                    : $"{settings.Minute} {settings.Hour} * * *",
                SimpleSettingsType.EveryWeek => format == CronFormat.Quartz
                    ? $"0 {settings.Minute} {settings.Hour} ? * {settings.DayOfWeek}"
                    : $"{settings.Minute} {settings.Hour} * * {settings.DayOfWeek}",
                SimpleSettingsType.EveryMonth => format == CronFormat.Quartz
                    ? $"0 {settings.Minute} {settings.Hour} {settings.DayOfMonth} * ?"
                    : $"{settings.Minute} {settings.Hour} {settings.DayOfMonth} * *",
                SimpleSettingsType.Custom => format == CronFormat.Quartz
                    ? $"{settings.Second} {settings.Minute} {settings.Hour} {settings.DayOfMonth} {settings.Month} {settings.DayOfWeek}"
                    : $"{settings.Minute} {settings.Hour} {settings.DayOfMonth} {settings.Month} {settings.DayOfWeek}",
                _ => throw new ArgumentException(localizer["Services:Errors:UnknownSettingsType"])
            };

            var validation = ValidateExpression(expression, format);
            if (validation.IsFailed(out var error))
            {
                return Res.Fail(localizer["Services:Errors:GeneratedExpressionInvalid", error.Message ?? string.Empty]);
            }

            return Res.Ok<string>(expression);
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:BuildExpressionFailed", ex.Message]);
        }
    }

    /// <summary>
    /// Convert expression format (Standard ↔ Quartz)
    /// </summary>
    public Res<string> ConvertFormat(string expression, CronFormat fromFormat, CronFormat toFormat)
    {
        if (fromFormat == toFormat)
        {
            return Res.Ok<string>(expression);
        }

        var validation = ValidateExpression(expression, fromFormat);
        if (validation.IsFailed(out var error))
        {
            return Res.Fail(localizer["Services:Errors:SourceExpressionInvalid", error.Message ?? string.Empty]);
        }

        try
        {
            if (fromFormat == CronFormat.Standard && toFormat == CronFormat.Quartz)
            {
                // Standard (5 segments) -> Quartz (6 segments): Add seconds (default is 0)
                return Res.Ok<string>($"0 {expression}");
            }
            else
            {
                // Quartz (6 segments) -> Standard (5 segments): Remove seconds digit
                var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 6)
                {
                    return Res.Fail(localizer["Services:Errors:QuartzFormatMustHave6Parts"]);
                }
                return Res.Ok<string>(string.Join(" ", parts.Skip(1)));
            }
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:FormatConversionFailed", ex.Message]);
        }
    }

}

/// <summary>
/// JavaScript return result model
/// </summary>
internal class CronParseResult
{
    public bool Success { get; set; }
    public string? Description { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Easy setup type
/// </summary>
public enum SimpleSettingsType
{
    EveryMinute,
    EveryHour,
    EveryDay,
    EveryWeek,
    EveryMonth,
    Custom
}

/// <summary>
/// Easy setup of Cron expressions
/// </summary>
public class SimpleSettings
{
    public SimpleSettingsType Type { get; set; } = SimpleSettingsType.EveryMinute;
    public int Second { get; set; } = 0;
    public int Minute { get; set; } = 0;
    public int Hour { get; set; } = 0;
    public string DayOfMonth { get; set; } = "*";
    public string Month { get; set; } = "*";
    public string DayOfWeek { get; set; } = "*";
}

/// <summary>
/// Cron expression display mode
/// </summary>
public enum CronDisplayMode
{
    /// <summary>
    /// Compact mode: Hover display description
    /// </summary>
    Compact,

    /// <summary>
    /// Full mode: display description directly
    /// </summary>
    Full
}
