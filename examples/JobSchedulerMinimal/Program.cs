using System.Text.Json.Serialization;
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

/// <summary>
/// On-demand example used to exercise durable admission, JSON arguments, and delayed availability in the UI.
/// </summary>
[JobConfig(
    JobName = "Generate sample report",
    Description = "Accepts a small JSON payload so the scheduler catalog can demonstrate operator-triggered work.",
    MaxConcurrency = 2,
    RetryCount = 1,
    MaxExecutionTimeoutSeconds = 120)]
public sealed class GenerateSampleReportJob(ILogger<GenerateSampleReportJob> logger)
    : TriggeredJob<GenerateSampleReportArgs>(logger)
{
    /// <inheritdoc />
    public override Task ExecuteAsync(GenerateSampleReportArgs parameters, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Generated sample report {ReportName} for {RequestedBy}.",
            parameters.ReportName,
            parameters.RequestedBy);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Defines the JSON contract accepted by <see cref="GenerateSampleReportJob"/>.
/// </summary>
public sealed record GenerateSampleReportArgs(
    [property: JsonPropertyName("reportName")] string ReportName,
    [property: JsonPropertyName("requestedBy")] string RequestedBy);
