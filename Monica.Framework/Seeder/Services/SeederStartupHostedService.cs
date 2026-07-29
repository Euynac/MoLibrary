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
        var descriptor = ExecutionDescriptor.ForInterface<ExecutionUnit, ExecutionUnit>(
            SeederExecutionPoints.Run,
            seederType,
            typeof(ISeeder),
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);

        await scope.ServiceProvider.GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(
                descriptor,
                ExecutionUnit.Value,
                seeder,
                () => seeder.SeedAsync(cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
