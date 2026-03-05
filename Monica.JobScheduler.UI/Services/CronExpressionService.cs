using Cronos;
using Monica.Tool.MoResponse;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monica.Core.Modules;
using Monica.JobScheduler.UI.Localization;

namespace Monica.JobScheduler.UI.Services;

/// <summary>
/// Cron 表达式格式类型
/// </summary>
public enum CronFormat
{
    /// <summary>
    /// 标准格式（5段）：分 时 日 月 周
    /// </summary>
    Standard,

    /// <summary>
    /// Quartz 格式（6段）：秒 分 时 日 月 周
    /// </summary>
    Quartz
}

/// <summary>
/// Cron 表达式服务，提供表达式解析、验证和执行时间计算
/// 注意：解析描述功能需要组件提供 JS 模块引用
/// </summary>
public class CronExpressionService(
    IStringLocalizer<JobSchedulerResource> localizer,
    IOptions<ModuleClockOption> clockOptions)
{
    private readonly TimeZoneInfo _cronTimeZone = clockOptions.Value.ConfiguredTimeZone ?? TimeZoneInfo.Local;

    /// <summary>
    /// 验证 Cron 表达式是否有效
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
            return true; // 隐式转换为 Res<bool>(true)
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:ExpressionFormatError", ex.Message].Value);
        }
    }

    /// <summary>
    /// 获取表达式的下 N 次执行时间
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

            return results; // 隐式转换为 Res<List<DateTime>>(results)
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:CalculateOccurrencesFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// 将表达式解析为可读的中文描述（使用 JavaScript cronstrue 库）
    /// </summary>
    /// <param name="jsModule">由组件提供的 JS 模块引用</param>
    /// <param name="expression">Cron 表达式</param>
    /// <param name="format">表达式格式</param>
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
            // 调用 JavaScript 函数
            var result = await jsModule.InvokeAsync<CronParseResult>(
                "parseCronExpression",
                expression,
                format == CronFormat.Quartz ? "quartz" : "standard");

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
    /// 从简易设置构建 Cron 表达式
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
                return Res.Fail(localizer["Services:Errors:GeneratedExpressionInvalid", error.Message]);
            }

            return Res.Ok<string>(expression);
        }
        catch (Exception ex)
        {
            return Res.Fail(localizer["Services:Errors:BuildExpressionFailed", ex.Message]);
        }
    }

    /// <summary>
    /// 转换表达式格式（Standard ↔ Quartz）
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
            return Res.Fail(localizer["Services:Errors:SourceExpressionInvalid", error.Message]);
        }

        try
        {
            if (fromFormat == CronFormat.Standard && toFormat == CronFormat.Quartz)
            {
                // Standard (5段) -> Quartz (6段)：添加秒位（默认为 0）
                return Res.Ok<string>($"0 {expression}");
            }
            else
            {
                // Quartz (6段) -> Standard (5段)：移除秒位
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
/// JavaScript 返回结果模型
/// </summary>
internal class CronParseResult
{
    public bool Success { get; set; }
    public string? Description { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 简易设置类型
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
/// Cron 表达式简易设置
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
/// Cron 表达式显示模式
/// </summary>
public enum CronDisplayMode
{
    /// <summary>
    /// 紧凑模式：悬浮显示描述
    /// </summary>
    Compact,

    /// <summary>
    /// 完整模式：直接显示描述
    /// </summary>
    Full
}
