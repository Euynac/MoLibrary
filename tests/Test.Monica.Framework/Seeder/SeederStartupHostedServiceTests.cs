using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Core.Execution;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Services;
using Xunit;

namespace Test.Monica.Framework.Seeder;

public sealed class SeederStartupHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WhenConfiguredToContinue_ShouldRunRemainingSeeders()
    {
        var executions = new List<string>();
        using var services = CreateServices(executions);
        var service = CreateHostedService(
            services,
            SeederFailureBehavior.ContinueStartup,
            typeof(FailingSeeder),
            typeof(SucceedingSeeder));

        await service.StartAsync(TestContext.Current.CancellationToken);

        executions.Should().Equal("failing", "succeeding");
    }

    [Fact]
    public async Task StartAsync_WhenConfiguredToFail_ShouldPreserveExceptionAndStopRemainingSeeders()
    {
        var executions = new List<string>();
        using var services = CreateServices(executions);
        var expected = services.GetRequiredService<SeederFailure>().Exception;
        var service = CreateHostedService(
            services,
            SeederFailureBehavior.FailStartup,
            typeof(FailingSeeder),
            typeof(SucceedingSeeder));

        Func<Task> start = () => service.StartAsync(TestContext.Current.CancellationToken);

        var assertion = await start.Should().ThrowAsync<SeederTestException>();
        assertion.Which.Should().BeSameAs(expected);
        executions.Should().Equal("failing");
    }

    private static ServiceProvider CreateServices(List<string> executions)
    {
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(executions)
            .AddSingleton<SeederFailure>()
            .AddScoped<IExecutionPipeline, PassThroughExecutionPipeline>()
            .AddTransient<FailingSeeder>()
            .AddTransient<SucceedingSeeder>()
            .BuildServiceProvider();
    }

    private static SeederStartupHostedService CreateHostedService(
        ServiceProvider services,
        SeederFailureBehavior behavior,
        params Type[] seederTypes)
    {
        return new SeederStartupHostedService(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<ILogger<SeederStartupHostedService>>(),
            seederTypes,
            behavior);
    }

    private sealed class PassThroughExecutionPipeline : IExecutionPipeline
    {
        public Task<TResult> ExecuteAsync<TInput, TResult>(
            ExecutionDescriptor descriptor,
            TInput input,
            object? target,
            ExecutionDelegate<TResult> terminal,
            CancellationToken cancellationToken = default,
            ExecutionFeatureCollection? features = null)
        {
            descriptor.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
            return terminal();
        }

        public Task ExecuteAsync<TInput>(
            ExecutionDescriptor descriptor,
            TInput input,
            object? target,
            Func<Task> terminal,
            CancellationToken cancellationToken = default,
            ExecutionFeatureCollection? features = null)
        {
            descriptor.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
            return terminal();
        }
    }

    private sealed class SeederFailure
    {
        public SeederTestException Exception { get; } = new("expected seeder failure");
    }

    private sealed class FailingSeeder(
        List<string> executions,
        SeederFailure failure) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken)
        {
            executions.Add("failing");
            return Task.FromException(failure.Exception);
        }
    }

    private sealed class SucceedingSeeder(List<string> executions) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken)
        {
            executions.Add("succeeding");
            return Task.CompletedTask;
        }
    }

    private sealed class SeederTestException(string message) : Exception(message);
}
