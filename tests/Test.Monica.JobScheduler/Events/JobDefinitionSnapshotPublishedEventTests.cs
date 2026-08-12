using System.Text.Json;
using AwesomeAssertions;
using Monica.Core.JsonSerialization.Converters;
using Monica.Core.JsonSerialization.Models;
using Monica.JobScheduler.Events;
using Monica.JobScheduler.Models;
using Xunit;

namespace Test.Monica.JobScheduler.Events;

public sealed class JobDefinitionSnapshotPublishedEventTests
{
    [Fact]
    public void Validate_AfterHostOwnedWallClockSerialization_ShouldPreserveCanonicalFingerprint()
    {
        const string schedulerScope = "snapshot-wire-tests";
        var definition = new JobDefinition
        {
            SchedulerScopeKey = schedulerScope,
            JobKey = "Worker.Project.ScheduledJob",
            FromProject = "Worker.Project",
            JobName = "Scheduled job",
            JobType = JobType.Recurring,
            MaxConcurrency = 1,
            RetryCount = 0,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5),
            CronExpression = "0 0 0 * * *",
            StartTime = new DateTime(2026, 8, 12, 1, 2, 3, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 8, 13, 4, 5, 6, DateTimeKind.Local)
        };
        var snapshot = JobDefinitionSnapshotPublishedEvent.Create(
            schedulerScope,
            definition.FromProject,
            sourceAppId: "worker-app",
            sourceInstanceId: "worker-instance",
            new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc),
            sourceReleaseVersion: "test",
            [definition]);
        var wireOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        wireOptions.Converters.Add(new DateTimeJsonConverter(DateTimeWireFormat.SpaceSeparatedWallClock));

        var payload = JsonSerializer.Serialize(snapshot, wireOptions);
        var received = JsonSerializer.Deserialize<JobDefinitionSnapshotPublishedEvent>(payload, wireOptions);

        received.Should().NotBeNull();
        received!.Invoking(candidate => candidate.Validate(schedulerScope)).Should().NotThrow();
        received.SourceBuildTimeUtc.Offset.Should().Be(TimeSpan.Zero);
    }
}
