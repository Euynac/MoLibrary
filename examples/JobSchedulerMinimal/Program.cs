using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Annotations;
using Monica.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.AddJobScheduler()
        .UseInMemoryStore()
        .UseSchedulerScope("job-scheduler-minimal")
        .UseCatalogRelease(
            "job-scheduler-minimal:development",
            deploymentGeneration: 1,
            [new("job-scheduler-minimal", "job-scheduler-minimal:development")])
        .AsStandalone()
        .UseLocalWorkerIdentity("job-scheduler-minimal", "job-scheduler-minimal:development");
    monica.AddJobSchedulerUI();
});

var app = builder.Build();

app.UseMonica();
app.MapGet("/", () => Results.Redirect("/job-scheduler"));
app.MapMonica();

app.Run();

/// <summary>
/// Recurring example job that gives the JobScheduler UI visible execution history.
/// </summary>
[JobConfig(
    JobName = "Minimal heartbeat",
    Description = "Writes a log entry every 30 seconds so the scheduler UI has a recurring job to display.",
    CronSchedule = "*/30 * * * * *")]
public sealed class MinimalHeartbeatJob(ILogger<MinimalHeartbeatJob> logger) : RecurringJob(logger)
{
    /// <inheritdoc />
    public override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Minimal heartbeat job ran at {Time:O}.", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
