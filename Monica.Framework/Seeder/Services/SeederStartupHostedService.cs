using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.Execution;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;

namespace Monica.Framework.Seeder.Services;

internal sealed class SeederStartupHostedService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SeederStartupHostedService> logger,
    IReadOnlyList<Type> seederTypes,
    SeederFailureBehavior failureBehavior) : IHostedService
{
    private static readonly ConcurrentDictionary<Type, ExecutionDescriptor> DESCRIPTORS = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var failureCount = 0;

        foreach (var seederType in seederTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ExecuteSeederAsync(seederType, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (failureBehavior == SeederFailureBehavior.ContinueStartup)
            {
                failureCount++;
                logger.LogError(exception, "Seeder {SeederType} failed during host startup", seederType.FullName);
            }
        }

        if (failureCount != 0)
        {
            logger.LogWarning(
                "Host startup continued after {FailureCount} of {SeederCount} seeders failed",
                failureCount,
                seederTypes.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task ExecuteSeederAsync(Type seederType, CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var seeder = (ISeeder)scope.ServiceProvider.GetRequiredService(seederType);
        var descriptor = DESCRIPTORS.GetOrAdd(seederType, CreateDescriptor);
        var context = new ExecutionContext<ExecutionUnit>(
            descriptor,
            ExecutionUnit.Value,
            seeder,
            cancellationToken);

        await scope.ServiceProvider.GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(context, async () =>
            {
                await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
                return ExecutionUnit.Value;
            })
            .ConfigureAwait(false);
    }

    private static ExecutionDescriptor CreateDescriptor(Type seederType)
    {
        var entryMethod = seederType.GetInterfaceMap(typeof(ISeeder)).TargetMethods.Single();
        return new ExecutionDescriptor(
            SeederExecutionPoints.Run,
            $"{seederType.FullName}.{entryMethod.Name}",
            seederType,
            entryMethod,
            typeof(ExecutionUnit),
            typeof(ExecutionUnit),
            isBusinessOperation: true,
            isLongRunning: false);
    }
}
