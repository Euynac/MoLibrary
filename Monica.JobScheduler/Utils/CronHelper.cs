using Cronos;

namespace Monica.JobScheduler.Utils;

/// <summary>
/// Helper methods for working with cron expressions.
/// </summary>
public static class CronHelper
{
    /// <summary>
    /// Parses a cron expression, automatically detecting whether it uses 5-segment (standard)
    /// or 6-segment (with seconds) format.
    /// </summary>
    /// <param name="cronExpression">The cron expression to parse.</param>
    /// <returns>A parsed <see cref="CronExpression"/> instance.</returns>
    /// <remarks>
    /// 5-segment format: minute hour day month dayOfWeek (standard cron)
    /// 6-segment format: second minute hour day month dayOfWeek (with seconds)
    /// </remarks>
    public static CronExpression Parse(string cronExpression)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var format = parts.Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
        return CronExpression.Parse(cronExpression, format);
    }
}
