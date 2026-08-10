using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;

namespace Monica.Framework.Seeder.Services.Support;

internal sealed class SeederScheduler(
    IServiceScopeFactory serviceScopeFactory,
    SeederGraph graph,
    SeederState state,
    ISeederRetryDelay retryDelay,
    IOptions<Monica.Modules.ModuleSeederOption> options)
{
    private readonly Monica.Modules.ModuleSeederOption _options = options.Value;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        state.MarkSchedulerStarted();
        var pending = graph.Nodes.ToDictionary(static node => node.SeederType);
        var running = new Dictionary<Type, RunningSeeder>();

        try
        {
            while (pending.Count > 0 || running.Count > 0)
            {
                BlockDependents(pending);

                if (!cancellationToken.IsCancellationRequested)
                {
                    ScheduleReadySeeders(pending, running, cancellationToken);
                }

                if (running.Count == 0)
                {
                    if (pending.Count == 0)
                    {
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        CancelPending(pending);
                        break;
                    }

                    throw new InvalidOperationException(
                        "Seeder scheduler reached a deadlock even though the dependency graph passed validation.");
                }

                await CompleteFinishedSeedersAsync(running).ConfigureAwait(false);
            }
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                CancelPending(pending);
            }

            state.MarkSchedulerCompleted();
        }
    }

    private void ScheduleReadySeeders(
        Dictionary<Type, SeederDescriptor> pending,
        Dictionary<Type, RunningSeeder> running,
        CancellationToken cancellationToken)
    {
        if (running.Values.Any(static execution => execution.Descriptor.ExecutionMode == SeederExecutionMode.Exclusive))
        {
            return;
        }

        var ready = pending.Values
            .Where(IsReady)
            .OrderBy(static descriptor => descriptor.SeederTypeName, StringComparer.Ordinal)
            .ToArray();

        foreach (var descriptor in ready)
        {
            if (running.Count >= _options.MaxConcurrency)
            {
                return;
            }

            if (descriptor.ExecutionMode == SeederExecutionMode.Exclusive)
            {
                // Encountering an exclusive node closes the current concurrent wave. The node starts only after
                // every earlier ready node has completed and no later ready node can pass the barrier.
                if (running.Count != 0)
                {
                    return;
                }

                Start(descriptor, pending, running, cancellationToken);
                return;
            }

            Start(descriptor, pending, running, cancellationToken);
        }
    }

    private bool IsReady(SeederDescriptor descriptor)
    {
        return descriptor.Dependencies.All(dependency => state.GetStatus(dependency) == SeederStatus.Succeeded);
    }

    private void Start(
        SeederDescriptor descriptor,
        Dictionary<Type, SeederDescriptor> pending,
        Dictionary<Type, RunningSeeder> running,
        CancellationToken cancellationToken)
    {
        pending.Remove(descriptor.SeederType);
        running.Add(
            descriptor.SeederType,
            new RunningSeeder(descriptor, ExecuteWithRetryAsync(descriptor, cancellationToken)));
    }

    private async Task CompleteFinishedSeedersAsync(Dictionary<Type, RunningSeeder> running)
    {
        await Task.WhenAny(running.Values.Select(static execution => execution.Task)).ConfigureAwait(false);
        var finished = running.Values
            .Where(static execution => execution.Task.IsCompleted)
            .OrderBy(static execution => execution.Descriptor.SeederTypeName, StringComparer.Ordinal)
            .ToArray();

        foreach (var execution in finished)
        {
            var result = await execution.Task.ConfigureAwait(false);
            running.Remove(execution.Descriptor.SeederType);
            switch (result.Status)
            {
                case SeederStatus.Succeeded:
                    state.MarkSucceeded(execution.Descriptor.SeederType);
                    break;
                case SeederStatus.Failed:
                    state.MarkFailed(execution.Descriptor.SeederType, result.Exception!);
                    break;
                case SeederStatus.Cancelled:
                    state.MarkCancelled(
                        execution.Descriptor.SeederType,
                        result.Exception?.Message ?? "Seeder execution was cancelled.");
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected terminal seeder status '{result.Status}'.");
            }
        }
    }

    private async Task<SeederExecutionResult> ExecuteWithRetryAsync(
        SeederDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= descriptor.MaxAttempts; attempt++)
        {
            state.MarkRunning(descriptor.SeederType, attempt);
            try
            {
                await ExecuteAttemptAsync(descriptor.SeederType, cancellationToken).ConfigureAwait(false);
                return SeederExecutionResult.Succeeded;
            }
            catch (OperationCanceledException exception)
            {
                return new SeederExecutionResult(SeederStatus.Cancelled, exception);
            }
            catch (Exception exception)
            {
                if (attempt == descriptor.MaxAttempts)
                {
                    return new SeederExecutionResult(SeederStatus.Failed, exception);
                }

                try
                {
                    await retryDelay.DelayAsync(attempt, _options, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException cancellationException)
                {
                    return new SeederExecutionResult(SeederStatus.Cancelled, cancellationException);
                }
            }
        }

        throw new InvalidOperationException("Seeder retry loop ended without producing a terminal result.");
    }

    private async Task ExecuteAttemptAsync(Type seederType, CancellationToken cancellationToken)
    {
        // Each attempt owns a fresh scope so failed transactions and scoped dependencies never leak into retries.
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

    private void BlockDependents(Dictionary<Type, SeederDescriptor> pending)
    {
        // Repeat until stable because blocking one node can immediately make another transitive dependent
        // unschedulable in the same scheduler turn.
        bool changed;
        do
        {
            changed = false;
            foreach (var descriptor in pending.Values
                         .OrderBy(static descriptor => descriptor.SeederTypeName, StringComparer.Ordinal)
                         .ToArray())
            {
                var unsuccessfulDependencies = descriptor.Dependencies
                    .Where(dependency => state.GetStatus(dependency) is
                        SeederStatus.Failed or SeederStatus.Blocked or SeederStatus.Cancelled)
                    .Select(dependency => graph.NodesByType[dependency].SeederTypeName)
                    .ToArray();
                if (unsuccessfulDependencies.Length == 0)
                {
                    continue;
                }

                pending.Remove(descriptor.SeederType);
                state.MarkBlocked(descriptor.SeederType, unsuccessfulDependencies);
                changed = true;
            }
        } while (changed);
    }

    private void CancelPending(Dictionary<Type, SeederDescriptor> pending)
    {
        foreach (var descriptor in pending.Values)
        {
            state.MarkCancelled(descriptor.SeederType, "Seeder execution was cancelled before it could start.");
        }

        pending.Clear();
    }

    private sealed record RunningSeeder(SeederDescriptor Descriptor, Task<SeederExecutionResult> Task);

    private sealed record SeederExecutionResult(SeederStatus Status, Exception? Exception)
    {
        public static SeederExecutionResult Succeeded { get; } = new(SeederStatus.Succeeded, null);
    }
}
