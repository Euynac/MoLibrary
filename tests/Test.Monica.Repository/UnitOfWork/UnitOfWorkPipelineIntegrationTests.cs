using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Execution;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Monica.Repository.UnitOfWork.Abstractions;
using Xunit;

namespace Test.Monica.Repository.UnitOfWork;

public sealed class UnitOfWorkPipelineIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ShouldApplyUnitOfWorkOnlyToAutomaticBoundaries(
        bool registerTransitively)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            if (registerTransitively)
            {
                monica.AddModule<TransitiveUnitOfWorkModule, TransitiveUnitOfWorkModuleOption>();
            }
            else
            {
                monica.AddUnitOfWork();
            }
        });
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

    public sealed class TransitiveUnitOfWorkModule : MonicaModule<TransitiveUnitOfWorkModuleOption>
    {
        public override void Describe(ModuleDescriptor module)
        {
            module.Require<ModuleUnitOfWork, ModuleUnitOfWorkOption>();
        }
    }

    public sealed class TransitiveUnitOfWorkModuleOption : ModuleOptions<TransitiveUnitOfWorkModule>;
}
