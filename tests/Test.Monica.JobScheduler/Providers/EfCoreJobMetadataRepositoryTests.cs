using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.EfCore;
using Monica.JobScheduler.Models;
using Monica.Modules;
using Monica.Repository.Persistence.Abstractions;
using Monica.Repository.Persistence.Services.Support;
using Monica.Repository.Snowflake.Abstractions;
using Monica.Testing.Repository;
using Xunit;

namespace Test.Monica.JobScheduler.Providers;

public sealed class EfCoreJobMetadataRepositoryTests
{
    private const string SCHEDULER_SCOPE = "ef-repository-tests";

    [Fact]
    public async Task SaveDefinitionAsync_WhenSoftDeletedDefinitionReturns_ShouldRestoreSameStableJobKey()
    {
        var options = Options.Create(new ModuleJobSchedulerOption());
        options.Value.SchedulerScopeKey = SCHEDULER_SCOPE;
        await using var fixture = await DbContextFixture<JobSchedulerDbContext>
            .UseSqliteInMemory(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<ISnowflakeIdGenerator>(new SequentialIdGenerator());
            })
            .EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var repository = new EfCoreJobMetadataRepository(
            new ScopedDbContextOperation<JobSchedulerDbContext>(
                new FixtureScopeFactory(fixture.Context)),
            options,
            NullLogger<EfCoreJobMetadataRepository>.Instance);
        var definition = CreateDefinition();

        await repository.SaveDefinitionAsync(definition, TestContext.Current.CancellationToken);
        definition.IsDeleted = true;
        definition.DeletedAt = DateTime.UtcNow;
        await repository.SaveDefinitionAsync(definition, TestContext.Current.CancellationToken);
        definition.IsDeleted = false;
        definition.DeletedAt = null;
        definition.JobName = "Restored declaration";

        await repository.SaveDefinitionAsync(definition, TestContext.Current.CancellationToken);

        fixture.Context.ChangeTracker.Clear();
        var entities = await fixture.Context.JobDefinitions
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken);
        entities.Should().ContainSingle();
        entities[0].JobKey.Should().Be(definition.JobKey);
        entities[0].JobName.Should().Be("Restored declaration");
        entities[0].IsDeleted.Should().BeFalse();
    }

    private static JobDefinition CreateDefinition()
    {
        return new JobDefinition
        {
            SchedulerScopeKey = SCHEDULER_SCOPE,
            JobKey = "Worker.Project.RestoredJob",
            FromProject = "Worker.Project",
            JobName = "Original declaration",
            JobType = JobType.Recurring,
            MaxConcurrency = 1,
            RetryCount = 0,
            MaxExecutionTimeout = TimeSpan.FromMinutes(5),
            CronExpression = "0 0 0 * * *"
        };
    }

    private sealed class FixtureScopeFactory(JobSchedulerDbContext context) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var services = new ServiceCollection();
            services.AddSingleton(context);
            return services.BuildServiceProvider().CreateScope();
        }
    }

    private sealed class SequentialIdGenerator : ISnowflakeIdGenerator
    {
        private long _value;

        public long GenerateId() => Interlocked.Increment(ref _value);
    }
}
