using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Providers;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.Providers;

public class InMemoryJobMetadataRepositoryTests
{
    [Fact]
    public async Task SaveDefinitionAsync_WhenQueryingDefinitions_ShouldStampScopeAndApplyProjectAndDeletionFilters()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var repository = CreateRepository("scope-a");

        var activeDefinition = CreateDefinition("jobs.alpha", "Project.A");
        var deletedDefinition = CreateDefinition("jobs.deleted", "Project.A", isDeleted: true);
        var otherProjectDefinition = CreateDefinition("jobs.beta", "Project.B");

        await repository.SaveDefinitionAsync(activeDefinition, cancellationToken);
        await repository.SaveDefinitionAsync(deletedDefinition, cancellationToken);
        await repository.SaveDefinitionAsync(otherProjectDefinition, cancellationToken);

        var queryResult = await repository.QueryDefinitionsAsync(new JobDefinitionQuery
        {
            FromProject = "Project.A",
            IncludeDeleted = false,
            PageNumber = 1,
            PageSize = 20
        }, cancellationToken);

        activeDefinition.SchedulerScopeKey.Should().Be("scope-a");
        deletedDefinition.SchedulerScopeKey.Should().Be("scope-a");
        otherProjectDefinition.SchedulerScopeKey.Should().Be("scope-a");

        queryResult.TotalCount.Should().Be(1);
        queryResult.Items.Should().ContainSingle();
        queryResult.Items[0].JobKey.Should().Be("jobs.alpha");
    }

    [Fact]
    public async Task QueryInstancesAsync_WhenProjectionAndStatisticsAreRequested_ShouldReturnOrderedItemsAndAllStates()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var repository = CreateRepository("scope-a");
        var now = DateTime.UtcNow;

        await repository.SaveInstanceAsync(CreateInstance("instance-succeeded", "jobs.alpha", JobState.Succeeded, now.AddMinutes(-10), completedAt: now.AddMinutes(-9)), cancellationToken);
        await repository.SaveInstanceAsync(CreateInstance("instance-failed", "jobs.alpha", JobState.Failed, now.AddMinutes(-5), completedAt: now.AddMinutes(-4)), cancellationToken);
        await repository.SaveInstanceAsync(CreateInstance("instance-processing", "jobs.alpha", JobState.Processing, now.AddMinutes(-2), startedAt: now.AddMinutes(-2)), cancellationToken);

        var projected = await repository.QueryInstancesAsync(
            new JobInstanceQuery
            {
                JobKey = "jobs.alpha",
                SortBy = "CreatedAt",
                SortDescending = true,
                PageNumber = 1,
                PageSize = 2
            },
            instance => instance.InstanceId,
            cancellationToken);

        var statistics = await repository.GetStateStatisticsAsync(cancellationToken: cancellationToken);

        projected.TotalCount.Should().Be(3);
        projected.Items.Should().Equal("instance-processing", "instance-failed");
        statistics[JobState.Succeeded].Should().Be(1);
        statistics[JobState.Failed].Should().Be(1);
        statistics[JobState.Processing].Should().Be(1);
        statistics[JobState.Cancelled].Should().Be(0);
    }

    private static InMemoryJobMetadataRepository CreateRepository(string scopeKey)
    {
        return new InMemoryJobMetadataRepository(
            NullLogger<InMemoryJobMetadataRepository>.Instance,
            Options.Create(new ModuleJobSchedulerOption
            {
                SchedulerScopeKey = scopeKey,
                ProjectName = "Test.Project"
            }));
    }

    private static JobDefinition CreateDefinition(string jobKey, string fromProject, bool isDeleted = false)
    {
        return new JobDefinition
        {
            SchedulerScopeKey = "original",
            JobKey = jobKey,
            FromProject = fromProject,
            JobName = jobKey,
            JobType = JobType.Recurring,
            CronExpression = "0 * * * *",
            IsDeleted = isDeleted
        };
    }

    private static JobInstance CreateInstance(
        string instanceId,
        string jobKey,
        JobState state,
        DateTime createdAt,
        DateTime? startedAt = null,
        DateTime? completedAt = null)
    {
        var instance = new JobInstance
        {
            InstanceId = instanceId,
            JobKey = jobKey
        };

        instance.RestoreFromPersistence(
            schedulerScopeKey: "original",
            state: state,
            createdAt: createdAt,
            startedAt: startedAt,
            completedAt: completedAt,
            scheduledExecutionTime: null,
            stateHistory: null,
            retryAttempt: 0,
            runningClientId: state == JobState.Processing ? "worker-a" : null);

        return instance;
    }
}
