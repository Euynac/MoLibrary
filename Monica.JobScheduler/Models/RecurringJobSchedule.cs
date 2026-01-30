using Cronos;

namespace Monica.JobScheduler.Models;

/// <summary>
/// Internal model tracking recurring job scheduling state.
/// </summary>
internal class RecurringJobSchedule
{
    public required string JobKey { get; init; }
    public required CronExpression CronExpression { get; init; }
    public Timer? Timer { get; set; }
    public DateTime NextOccurrence { get; set; }
}
