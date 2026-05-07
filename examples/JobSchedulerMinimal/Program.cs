using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

Mo.AddJobScheduler()
    .UseInMemoryMetadataRepository()
    .UseSchedulerScope("job-scheduler-minimal")
    .UseInMemoryProvider();
Mo.AddJobSchedulerUI();

builder.UseMonica();

var app = builder.Build();

app.UseMonica();
app.MapGet("/", () => Results.Redirect("/job-scheduler"));
app.MapMonica();

app.Run();

/// <summary>
/// Recurring example job that gives the JobScheduler dashboard a visible execution history.
/// </summary>
[JobConfig(
    JobName = "Minimal heartbeat",
    Description = "Writes a log entry every 30 seconds so the dashboard has a recurring job to display.",
    CronSchedule = "*/30 * * * * *")]
public sealed class MinimalHeartbeatJob(ILogger<MinimalHeartbeatJob> logger) : RecurringJob
{
    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Minimal heartbeat job ran at {Time:O}.", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
