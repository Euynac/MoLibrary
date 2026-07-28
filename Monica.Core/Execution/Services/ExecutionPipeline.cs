using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Execution.Models.Internal;

namespace Monica.Core.Execution.Services;

internal sealed class ExecutionPipeline(
    IServiceProvider serviceProvider,
    ExecutionBehaviorPlanCache planCache) : IExecutionPipeline
{
    public Task<TResult> ExecuteAsync<TInput, TResult>(
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> terminal)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminal);

        if (context.Descriptor.ResultType != typeof(TResult))
        {
            throw new ArgumentException(
                $"Execution descriptor result type '{context.Descriptor.ResultType.FullName}' does not match pipeline " +
                $"result type '{typeof(TResult).FullName}'.",
                nameof(context));
        }

        var plan = planCache.GetPlan<TInput, TResult>(context.Descriptor);
        var next = terminal;

        for (var index = plan.Count - 1; index >= 0; index--)
        {
            var behaviorPlan = plan[index];
            var remainingPipeline = next;
            next = () => InvokeBehaviorAsync(behaviorPlan, context, remainingPipeline);
        }

        return next();
    }

    private Task<TResult> InvokeBehaviorAsync<TInput, TResult>(
        ExecutionBehaviorPlan plan,
        ExecutionContext<TInput> context,
        ExecutionDelegate<TResult> next)
    {
        var behavior = serviceProvider.GetRequiredKeyedService(
            plan.ImplementationType,
            plan.Key);

        if (behavior is not IExecutionBehavior<TInput, TResult> typedBehavior)
        {
            throw new InvalidOperationException(
                $"Resolved execution behavior '{plan.ImplementationType.FullName}' does not implement " +
                $"IExecutionBehavior<{typeof(TInput).FullName}, {typeof(TResult).FullName}>.");
        }

        var invocation = new SingleInvocationExecutionDelegate<TResult>(next, plan.Key);
        return typedBehavior.ExecuteAsync(context, invocation.InvokeAsync);
    }

    private sealed class SingleInvocationExecutionDelegate<TResult>(
        ExecutionDelegate<TResult> next,
        string behaviorKey)
    {
        private int _invocationCount;

        public Task<TResult> InvokeAsync()
        {
            if (Interlocked.Increment(ref _invocationCount) != 1)
            {
                throw new InvalidOperationException(
                    $"Execution behavior '{behaviorKey}' invoked the remaining pipeline more than once.");
            }

            return next();
        }
    }
}
