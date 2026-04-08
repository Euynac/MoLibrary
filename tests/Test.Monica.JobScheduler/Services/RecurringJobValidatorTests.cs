using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Services;
using NSubstitute;
using Xunit;

namespace Test.Monica.JobScheduler.Services;

public class RecurringJobValidatorTests
{
    [Fact]
    public async Task ValidateRecurringJobAsync_WhenDefinitionIsDeleted_ShouldRemoveScheduleAndReturnNull()
    {
        var cacheService = Substitute.For<IJobDefinitionCacheService>();
        var validator = new RecurringJobValidator(cacheService, NullLogger<RecurringJobValidator>.Instance);
        var removedKeys = new List<string>();

        var result = await validator.ValidateRecurringJobAsync(
            CreateDefinition(isDeleted: true),
            jobKey =>
            {
                removedKeys.Add(jobKey);
                return Task.CompletedTask;
            });

        result.Should().BeNull();
        removedKeys.Should().Equal("jobs.alpha");
    }

    [Fact]
    public async Task ValidateRecurringJobAsync_WhenJobIsBeforeStartTime_ShouldInvokeCallbackRemoveScheduleAndReturnNull()
    {
        var cacheService = Substitute.For<IJobDefinitionCacheService>();
        var validator = new RecurringJobValidator(cacheService, NullLogger<RecurringJobValidator>.Instance);
        var removedKeys = new List<string>();
        var deferredDefinitions = new List<JobDefinition>();

        var definition = CreateDefinition(startTime: DateTime.UtcNow.AddMinutes(30));

        var result = await validator.ValidateRecurringJobAsync(
            definition,
            jobKey =>
            {
                removedKeys.Add(jobKey);
                return Task.CompletedTask;
            },
            scheduledDefinition =>
            {
                deferredDefinitions.Add(scheduledDefinition);
                return Task.CompletedTask;
            });

        result.Should().BeNull();
        removedKeys.Should().Equal("jobs.alpha");
        deferredDefinitions.Should().ContainSingle();
        deferredDefinitions[0].Should().BeSameAs(definition);
    }

    private static JobDefinition CreateDefinition(bool isDeleted = false, DateTime? startTime = null)
    {
        return new JobDefinition
        {
            SchedulerScopeKey = "scope-a",
            JobKey = "jobs.alpha",
            FromProject = "Test.Project",
            JobName = "Alpha Job",
            JobType = JobType.Recurring,
            CronExpression = "0 * * * *",
            IsDeleted = isDeleted,
            StartTime = startTime
        };
    }
}
