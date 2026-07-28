using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.Repository.UnitOfWork.Abstractions;
using Xunit;

namespace Test.Monica.Repository.UnitOfWork;

public sealed class UnitOfWorkPipelineIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldApplyUnitOfWorkOnlyToAutomaticBoundaries()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica => monica.AddUnitOfWork());
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IExecutionPipeline>();
        var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var cancellationToken = TestContext.Current.CancellationToken;
        var automaticSawAmbientScope = false;
        var noneSawAmbientScope = false;

        await pipeline.ExecuteAsync(
            CreateDescriptor(ExecutionTransactionMode.Automatic),
            ExecutionUnit.Value,
            target: null,
            () =>
            {
                automaticSawAmbientScope = manager.Current is not null;
                return Task.CompletedTask;
            },
            cancellationToken);

        manager.Current.Should().BeNull();

        await pipeline.ExecuteAsync(
            CreateDescriptor(ExecutionTransactionMode.None),
            ExecutionUnit.Value,
            target: null,
            () =>
            {
                noneSawAmbientScope = manager.Current is not null;
                return Task.CompletedTask;
            },
            cancellationToken);

        automaticSawAmbientScope.Should().BeTrue();
        noneSawAmbientScope.Should().BeFalse();
        manager.Current.Should().BeNull();
    }

    private static ExecutionDescriptor CreateDescriptor(ExecutionTransactionMode transactionMode)
    {
        return ExecutionDescriptor.ForMethod<ExecutionUnit, ExecutionUnit>(
            new ExecutionPoint("test.unit-of-work-selection"),
            typeof(UnitOfWorkPipelineIntegrationTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: transactionMode);
    }
}
