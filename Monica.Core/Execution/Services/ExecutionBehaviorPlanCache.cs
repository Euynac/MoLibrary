using System.Collections.Concurrent;
using System.Collections.Immutable;
using Monica.Core.Execution.Abstractions;
using Monica.Core.Execution.Models;
using Monica.Core.Execution.Models.Internal;

namespace Monica.Core.Execution.Services;

internal sealed class ExecutionBehaviorPlanCache : IExecutionPipelineCatalog
{
    private readonly ConcurrentDictionary<ExecutionDescriptor, ExecutionBehaviorPlan> _plans =
        new(ReferenceEqualityComparer.Instance);
    private readonly ExecutionBehaviorRegistration[] _registrations;
    private readonly ImmutableArray<ExecutionBehaviorRegistrationSnapshot> _registrationSnapshots;

    public ExecutionBehaviorPlanCache(
        IReadOnlyList<ExecutionBehaviorRegistration> registrations,
        ExecutionBehaviorServiceRegistrationValidator registrationValidator)
    {
        _ = registrationValidator;
        _registrations = registrations
            .OrderBy(static registration => registration.Order)
            .ThenBy(static registration => registration.SortName, StringComparer.Ordinal)
            .ToArray();
        _registrationSnapshots = _registrations
            .Select(static registration => registration.CreateSnapshot())
            .ToImmutableArray();
    }

    public ExecutionBehaviorPlan GetPlan(ExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return _plans.GetOrAdd(
            descriptor,
            static (key, state) => new ExecutionBehaviorPlan(key, () => state.BuildPlan(key)),
            this);
    }

    public ExecutionPipelineCatalogSnapshot GetSnapshot()
    {
        var plans = _plans.Values
            .Select(static plan => plan.CreateSnapshot())
            .OrderBy(static plan => plan.Descriptor.Point.Value, StringComparer.Ordinal)
            .ThenBy(static plan => plan.Descriptor.DisplayName, StringComparer.Ordinal)
            .ThenBy(static plan => plan.PlanKey, StringComparer.Ordinal)
            .ToImmutableArray();

        return new ExecutionPipelineCatalogSnapshot(
            DateTimeOffset.UtcNow,
            _registrationSnapshots,
            plans);
    }

    public ExecutionPipelinePlanSnapshot InspectPlan(ExecutionDescriptor descriptor)
    {
        return GetPlan(descriptor).Inspect();
    }

    private ExecutionBehaviorPlanContent BuildPlan(ExecutionDescriptor descriptor)
    {
        var behaviorTypes = new List<Type>(_registrations.Length);
        var behaviors = ImmutableArray.CreateBuilder<ExecutionPipelineAppliedBehaviorSnapshot>(_registrations.Length);

        foreach (var registration in _registrations)
        {
            var behaviorType = registration.TryCreatePlan(
                descriptor,
                descriptor.InputType,
                descriptor.ResultType);
            if (behaviorType is null)
            {
                continue;
            }

            behaviorTypes.Add(behaviorType);
            behaviors.Add(registration.CreateAppliedSnapshot(behaviorTypes.Count, behaviorType));
        }

        return new ExecutionBehaviorPlanContent(behaviorTypes.ToArray(), behaviors.ToImmutable());
    }
}
