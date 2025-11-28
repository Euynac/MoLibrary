using Cronos;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.JobScheduler.UI.Services;

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
/// </summary>
public class CronExpressionService
{
    /// <summary>
    /// 验证 Cron 表达式是否有效
    /// </summary>
    public Res<bool> ValidateExpression(string expression, CronFormat format)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "表达式不能为空";
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
            return $"表达式格式错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取表达式的下 N 次执行时间
    /// </summary>
    public Res<List<DateTime>> GetNextOccurrences(string expression, CronFormat format, int count = 5, DateTime? fromTime = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "表达式不能为空";
        }

        if (count <= 0 || count > 100)
        {
            return "计算次数必须在 1-100 之间";
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
                var next = cronExpression.GetNextOccurrence(currentTime, TimeZoneInfo.Local);
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
            return $"计算执行时间失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 将表达式解析为可读的中文描述
    /// </summary>
    public Res<string> ParseToDescription(string expression, CronFormat format)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Res.Fail("表达式不能为空");
        }

        try
        {
            var cronFormat = format == CronFormat.Quartz
                ? Cronos.CronFormat.IncludeSeconds
                : Cronos.CronFormat.Standard;

            var cronExpression = CronExpression.Parse(expression, cronFormat);
            var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (format == CronFormat.Quartz && parts.Length != 6)
            {
                return Res.Fail("Quartz 格式需要 6 段（秒 分 时 日 月 周）");
            }

            if (format == CronFormat.Standard && parts.Length != 5)
            {
                return Res.Fail("Standard 格式需要 5 段（分 时 日 月 周）");
            }

            var description = format == CronFormat.Quartz
                ? BuildQuartzDescription(parts)
                : BuildStandardDescription(parts);

            return Res.Ok(description);
        }
        catch (Exception ex)
        {
            return Res.Fail($"解析失败: {ex.Message}");
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
                _ => throw new ArgumentException("未知的设置类型")
            };

            var validation = ValidateExpression(expression, format);
            if (validation.IsFailed(out var error))
            {
                return Res.Fail($"生成的表达式无效: {error.Message}");
            }

            return Res.Ok(expression);
        }
        catch (Exception ex)
        {
            return Res.Fail($"构建表达式失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 转换表达式格式（Standard ↔ Quartz）
    /// </summary>
    public Res<string> ConvertFormat(string expression, CronFormat fromFormat, CronFormat toFormat)
    {
        if (fromFormat == toFormat)
        {
            return Res.Ok(expression);
        }

        var validation = ValidateExpression(expression, fromFormat);
        if (validation.IsFailed(out var error))
        {
            return Res.Fail($"源表达式无效: {error.Message}");
        }

        try
        {
            if (fromFormat == CronFormat.Standard && toFormat == CronFormat.Quartz)
            {
                // Standard (5段) -> Quartz (6段)：添加秒位（默认为 0）
                return Res.Ok($"0 {expression}");
            }
            else
            {
                // Quartz (6段) -> Standard (5段)：移除秒位
                var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 6)
                {
                    return Res.Fail("Quartz 格式必须包含 6 段");
                }
                return Res.Ok(string.Join(" ", parts.Skip(1)));
            }
        }
        catch (Exception ex)
        {
            return Res.Fail($"格式转换失败: {ex.Message}");
        }
    }

    private string BuildQuartzDescription(string[] parts)
    {
        var descriptions = new List<string>();

        // 秒
        if (parts[0] != "*" && parts[0] != "?")
        {
            descriptions.Add($"秒: {DescribePart(parts[0], "秒")}");
        }

        // 分
        if (parts[1] != "*")
        {
            descriptions.Add($"分: {DescribePart(parts[1], "分")}");
        }

        // 时
        if (parts[2] != "*")
        {
            descriptions.Add($"时: {DescribePart(parts[2], "时")}");
        }

        // 日
        if (parts[3] != "*" && parts[3] != "?")
        {
            descriptions.Add($"日: {DescribePart(parts[3], "日")}");
        }

        // 月
        if (parts[4] != "*")
        {
            descriptions.Add($"月: {DescribePart(parts[4], "月")}");
        }

        // 周
        if (parts[5] != "*" && parts[5] != "?")
        {
            descriptions.Add($"周: {DescribePart(parts[5], "周")}");
        }

        return descriptions.Count > 0
            ? string.Join(", ", descriptions)
            : "每秒执行";
    }

    private string BuildStandardDescription(string[] parts)
    {
        var descriptions = new List<string>();

        // 分
        if (parts[0] != "*")
        {
            descriptions.Add($"分: {DescribePart(parts[0], "分")}");
        }

        // 时
        if (parts[1] != "*")
        {
            descriptions.Add($"时: {DescribePart(parts[1], "时")}");
        }

        // 日
        if (parts[2] != "*")
        {
            descriptions.Add($"日: {DescribePart(parts[2], "日")}");
        }

        // 月
        if (parts[3] != "*")
        {
            descriptions.Add($"月: {DescribePart(parts[3], "月")}");
        }

        // 周
        if (parts[4] != "*")
        {
            descriptions.Add($"周: {DescribePart(parts[4], "周")}");
        }

        return descriptions.Count > 0
            ? string.Join(", ", descriptions)
            : "每分钟执行";
    }

    private string DescribePart(string part, string unit)
    {
        if (part.StartsWith("*/"))
        {
            return $"每 {part[2..]} {unit}";
        }

        if (part.Contains("-"))
        {
            var range = part.Split('-');
            return $"{range[0]}-{range[1]} {unit}";
        }

        if (part.Contains(","))
        {
            return $"{part} {unit}";
        }

        return $"{part} {unit}";
    }
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
