using Microsoft.Extensions.DependencyInjection;

namespace Monica.Core.Execution.Services;

internal sealed class ExecutionPipeline(
    IServiceProvider serviceProvider,
    ExecutionBehaviorPlanCache planCache) : IExecutionPipeline
{
    public Task<TResult> ExecuteAsync<TInput, TResult>(
        ExecutionDescriptor descriptor,
        TInput input,
        object? target,
        ExecutionDelegate<TResult> terminal,
        CancellationToken cancellationToken = default,
        ExecutionFeatureCollection? features = null)
    {
        ValidateDescriptor<TInput, TResult>(descriptor);
        ArgumentNullException.ThrowIfNull(terminal);

        var plan = planCache.GetPlan(descriptor);
        if (plan.Length == 0)
        {
            return terminal();
        }

        var context = new ExecutionContext<TInput>(descriptor, input, target, cancellationToken, features);
        return ExecuteCore(context, terminal, plan);
    }

    public Task ExecuteAsync<TInput>(
        ExecutionDescriptor descriptor,
        TInput input,
        object? target,
        Func<Task> terminal,
        CancellationToken cancellationToken = default,
        ExecutionFeatureCollection? features = null)
    {
        ValidateDescriptor<TInput, ExecutionUnit>(descriptor);
        ArgumentNullException.ThrowIfNull(terminal);

        var plan = planCache.GetPlan(descriptor);
        if (plan.Length == 0)
        {
            return terminal();
        }

        var context = new ExecutionContext<TInput>(descriptor, input, target, cancellationToken, features);
        return ExecuteWithoutResultCore(context, terminal, plan);
    }

    private Task<TResult> ExecuteCore<TInput, TResult>(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> terminal,
        IReadOnlyList<Type> plan)
    {
        var next = terminal;

        for (var index = plan.Count - 1; index >= 0; index--)
        {
            var behaviorType = plan[index];
            var remainingPipeline = next;
            next = () => InvokeBehaviorAsync(behaviorType, context, remainingPipeline);
        }

        return next();
    }

    private async Task ExecuteWithoutResultCore<TInput>(
        ExecutionContext<TInput> context,
        Func<Task> terminal,
        IReadOnlyList<Type> plan)
    {
        await ExecuteCore(
                context,
                async () =>
                {
                    await terminal().ConfigureAwait(false);
                    return ExecutionUnit.Value;
                },
                plan)
            .ConfigureAwait(false);
    }

    private Task<TResult> InvokeBehaviorAsync<TInput, TResult>(
        Type behaviorType,
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> next)
    {
        var behavior = serviceProvider.GetRequiredService(behaviorType);

        if (behavior is not IExecutionBehavior<TInput, TResult> typedBehavior)
        {
            throw new InvalidOperationException(
                $"Resolved execution behavior '{behaviorType.FullName}' does not implement " +
                $"IExecutionBehavior<{typeof(TInput).FullName}, {typeof(TResult).FullName}>.");
        }

        var invocation = new SingleInvocationExecutionDelegate<TResult>(next, behaviorType);
        return typedBehavior.ExecuteAsync(context, invocation.InvokeAsync);
    }

    private static void ValidateDescriptor<TInput, TResult>(ExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.InputType != typeof(TInput))
        {
            throw new ArgumentException(
                $"Execution descriptor input type '{descriptor.InputType.FullName}' does not match pipeline input type " +
                $"'{typeof(TInput).FullName}'.",
                nameof(descriptor));
        }

        if (descriptor.ResultType != typeof(TResult))
        {
            throw new ArgumentException(
                $"Execution descriptor result type '{descriptor.ResultType.FullName}' does not match pipeline result " +
                $"type '{typeof(TResult).FullName}'.",
                nameof(descriptor));
        }
    }

    private sealed class SingleInvocationExecutionDelegate<TResult>(
        ExecutionDelegate<TResult> next,
        Type behaviorType)
    {
        private int _invoked;

        public Task<TResult> InvokeAsync()
        {
            if (Interlocked.Exchange(ref _invoked, 1) != 0)
            {
                throw new InvalidOperationException(
                    $"Execution behavior '{behaviorType.FullName}' invoked the remaining pipeline more than once.");
            }

            return next();
        }
    }
}
