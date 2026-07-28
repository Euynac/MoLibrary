using System.Collections.Concurrent;
using Monica.Core.Execution.Models.Internal;

namespace Monica.Core.Execution.Services;

internal sealed class ExecutionBehaviorPlanCache
{
    private readonly ConcurrentDictionary<ExecutionPlanCacheKey, ExecutionBehaviorPlan[]> _plans = new();
    private readonly ExecutionBehaviorRegistration[] _registrations;

    public ExecutionBehaviorPlanCache(IReadOnlyList<ExecutionBehaviorRegistration> registrations)
    {
        _registrations = registrations
            .OrderBy(static registration => registration.Order)
            .ThenBy(static registration => registration.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<ExecutionBehaviorPlan> GetPlan<TInput, TResult>(ExecutionDescriptor descriptor)
    {
        var key = new ExecutionPlanCacheKey(descriptor, typeof(TInput), typeof(TResult));
        return _plans.GetOrAdd(key, static (cacheKey, state) => state.BuildPlan(cacheKey), this);
    }

    private ExecutionBehaviorPlan[] BuildPlan(ExecutionPlanCacheKey key)
    {
        return _registrations
            .Select(registration => registration.TryCreatePlan(
                key.Descriptor,
                key.InputType,
                key.ResultType))
            .OfType<ExecutionBehaviorPlan>()
            .ToArray();
    }

    private readonly record struct ExecutionPlanCacheKey(
        ExecutionDescriptor Descriptor,
        Type InputType,
        Type ResultType);
}
