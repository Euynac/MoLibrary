using System.Collections.Concurrent;
using Monica.Core.Execution.Models.Internal;

namespace Monica.Core.Execution.Services;

internal sealed class ExecutionBehaviorPlanCache
{
    private readonly ConcurrentDictionary<ExecutionDescriptor, Type[]> _plans =
        new(ReferenceEqualityComparer.Instance);
    private readonly ExecutionBehaviorRegistration[] _registrations;

    public ExecutionBehaviorPlanCache(
        IReadOnlyList<ExecutionBehaviorRegistration> registrations,
        ExecutionBehaviorServiceRegistrationValidator registrationValidator)
    {
        _ = registrationValidator;
        _registrations = registrations
            .OrderBy(static registration => registration.Order)
            .ThenBy(static registration => registration.SortName, StringComparer.Ordinal)
            .ToArray();
    }

    public Type[] GetPlan(ExecutionDescriptor descriptor)
    {
        return _plans.GetOrAdd(descriptor, static (key, state) => state.BuildPlan(key), this);
    }

    private Type[] BuildPlan(ExecutionDescriptor descriptor)
    {
        return _registrations
            .Select(registration => registration.TryCreatePlan(
                descriptor,
                descriptor.InputType,
                descriptor.ResultType))
            .OfType<Type>()
            .ToArray();
    }
}
