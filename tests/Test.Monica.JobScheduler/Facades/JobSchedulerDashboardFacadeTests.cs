using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Core.Results;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Providers;
using Monica.Modules;
using NSubstitute;
using Test.Monica.Results;
using Xunit;

namespace Test.Monica.JobScheduler.Facades;

public class JobSchedulerDashboardFacadeTests
{
    [Fact]
    public async Task GetDashboardAsync_WhenAllDependenciesSucceed_ShouldBuildSummaryAndProblemBuckets()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var repository = CreateRepository();
        var definitions = new[]
        {
            CreateDefinition("jobs.alpha", "Alpha Job", JobType.Recurring, maxExecutionTimeout: TimeSpan.FromHours(1)),
            CreateDefinition("jobs.beta", "Beta Job", JobType.Triggered, maxExecutionTimeout: TimeSpan.FromHours(1)),
            CreateDefinition("jobs.gamma", "Gamma Job", JobType.Triggered, isDisabled: true)
        };

        foreach (var instance in CreateDashboardInstances())
        {
            await repository.SaveInstanceAsync(instance, cancellationToken);
        }

        var cacheService = Substitute.For<IJobDefinitionCacheService>();
        cacheService.GetAllDefinitionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<JobDefinition>>(definitions));

        var concurrencyGuard = Substitute.For<IJobConcurrencyGuard>();
        concurrencyGuard.GetAllExecutionStatisticsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, JobExecutionStatisticSnapshot>>(new Dictionary<string, JobExecutionStatisticSnapshot>
            {
                ["jobs.beta"] = new()
                {
                    JobKey = "jobs.beta",
                    MaxConcurrency = 2,
                    RunningCount = 1,
                    PendingCount = 0
                }
            }));

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddHealthChecks()
            .AddCheck("JobScheduler", () => HealthCheckResult.Healthy("Scheduler healthy"))
            .Services
            .BuildServiceProvider();

        var facade = new JobSchedulerDashboardFacade(
            cacheService,
            repository,
            concurrencyGuard,
            provider.GetRequiredService<HealthCheckService>(),
            NullLogger<JobSchedulerDashboardFacade>.Instance);

        var result = await facade.GetDashboardAsync(TimeSpan.FromHours(24), recentActivityCount: 5, cancellationToken: cancellationToken);
        var snapshot = result.ShouldSucceed();

        snapshot.Should().NotBeNull();
        snapshot!.Summary.TotalJobs.Should().Be(3);
        snapshot.Summary.RecurringJobCount.Should().Be(1);
        snapshot.Summary.TriggeredJobCount.Should().Be(2);
        snapshot.Summary.DisabledJobCount.Should().Be(1);
        snapshot.Summary.RunningNow.Should().Be(1);
        snapshot.Summary.HealthStatus.Should().Be(SystemHealthStatus.Healthy);
        snapshot.Summary.HealthMessage.Should().Be("Scheduler healthy");
        snapshot.Summary.StateDistribution[JobState.Succeeded].Should().Be(7);
        snapshot.Summary.StateDistribution[JobState.Skipped].Should().Be(3);
        snapshot.Summary.StateDistribution[JobState.Processing].Should().Be(1);
        snapshot.Summary.SuccessRate.Should().Be(53.8);
        snapshot.Summary.ThroughputPerHour.Should().Be(1);
        snapshot.Problems.ConsecutiveFailures.Should().ContainSingle();
        snapshot.Problems.LongRunning.Should().ContainSingle();
        snapshot.Problems.HighSkipRate.Should().ContainSingle();
        snapshot.RecentActivities.Should().HaveCount(5);
        snapshot.RecentActivities.Should().Contain(activity => activity.JobKey == "jobs.alpha");
        snapshot.RecentActivities.Should().Contain(activity => activity.JobKey == "jobs.beta");
    }

    [Fact]
    public async Task GetDashboardAsync_WhenARequiredDependencyThrows_ShouldReturnFailureResult()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var repository = CreateRepository();

        var cacheService = Substitute.For<IJobDefinitionCacheService>();
        cacheService.GetAllDefinitionsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<IReadOnlyList<JobDefinition>>(new InvalidOperationException("cache unavailable")));

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddHealthChecks()
            .AddCheck("JobScheduler", () => HealthCheckResult.Healthy("ok"))
            .Services
            .BuildServiceProvider();

        var facade = new JobSchedulerDashboardFacade(
            cacheService,
            repository,
            Substitute.For<IJobConcurrencyGuard>(),
            provider.GetRequiredService<HealthCheckService>(),
            NullLogger<JobSchedulerDashboardFacade>.Instance);

        var result = await facade.GetDashboardAsync(TimeSpan.FromHours(24), cancellationToken: cancellationToken);

        result.Status.Should().Be(ResStatus.BadRequest);
        result.Message.Should().Contain("Failed to build dashboard snapshot");
        result.Message.Should().Contain("cache unavailable");
        result.Data.Should().BeNull();
    }

    private static InMemoryJobMetadataRepository CreateRepository()
    {
        return new InMemoryJobMetadataRepository(
            NullLogger<InMemoryJobMetadataRepository>.Instance,
            Microsoft.Extensions.Options.Options.Create(new ModuleJobSchedulerOption
            {
                SchedulerScopeKey = "dashboard-scope",
                ProjectName = "Test.Project"
            }));
    }

    private static IReadOnlyList<JobInstance> CreateDashboardInstances()
    {
        var now = DateTime.UtcNow;

        return
        [
            CreateInstance("alpha-fail-1", "jobs.alpha", JobState.Failed, now.AddMinutes(-10), completedAt: now.AddMinutes(-9)),
            CreateInstance("alpha-fail-2", "jobs.alpha", JobState.Failed, now.AddMinutes(-20), completedAt: now.AddMinutes(-19)),
            CreateInstance("alpha-fail-3", "jobs.alpha", JobState.Terminated, now.AddMinutes(-30), completedAt: now.AddMinutes(-29)),
            CreateInstance("beta-running", "jobs.beta", JobState.Processing, now.AddMinutes(-50), startedAt: now.AddMinutes(-40)),
            CreateInstance("beta-skip-1", "jobs.beta", JobState.Skipped, now.AddHours(-10), completedAt: now.AddHours(-10).AddMinutes(1)),
            CreateInstance("beta-skip-2", "jobs.beta", JobState.Skipped, now.AddHours(-11), completedAt: now.AddHours(-11).AddMinutes(1)),
            CreateInstance("beta-skip-3", "jobs.beta", JobState.Skipped, now.AddHours(-12), completedAt: now.AddHours(-12).AddMinutes(1)),
            CreateInstance("beta-success-1", "jobs.beta", JobState.Succeeded, now.AddHours(-13), completedAt: now.AddHours(-13).AddMinutes(1)),
            CreateInstance("beta-success-2", "jobs.beta", JobState.Succeeded, now.AddHours(-14), completedAt: now.AddHours(-14).AddMinutes(1)),
            CreateInstance("beta-success-3", "jobs.beta", JobState.Succeeded, now.AddHours(-15), completedAt: now.AddHours(-15).AddMinutes(1)),
            CreateInstance("beta-success-4", "jobs.beta", JobState.Succeeded, now.AddHours(-16), completedAt: now.AddHours(-16).AddMinutes(1)),
            CreateInstance("beta-success-5", "jobs.beta", JobState.Succeeded, now.AddHours(-17), completedAt: now.AddHours(-17).AddMinutes(1)),
            CreateInstance("beta-success-6", "jobs.beta", JobState.Succeeded, now.AddHours(-18), completedAt: now.AddHours(-18).AddMinutes(1)),
            CreateInstance("beta-success-7", "jobs.beta", JobState.Succeeded, now.AddHours(-19), completedAt: now.AddHours(-19).AddMinutes(1))
        ];
    }

    private static JobDefinition CreateDefinition(
        string jobKey,
        string jobName,
        JobType jobType,
        bool isDisabled = false,
        TimeSpan? maxExecutionTimeout = null)
    {
        return new JobDefinition
        {
            SchedulerScopeKey = "dashboard-scope",
            JobKey = jobKey,
            FromProject = "Test.Project",
            JobName = jobName,
            JobType = jobType,
            MaxExecutionTimeout = maxExecutionTimeout ?? TimeSpan.FromHours(1),
            IsDisabled = isDisabled,
            CronExpression = jobType == JobType.Recurring ? "0 * * * *" : null
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
            schedulerScopeKey: "dashboard-scope",
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
