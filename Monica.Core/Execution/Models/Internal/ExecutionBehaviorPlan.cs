using System.Collections.Immutable;
using Monica.Core.Extensions;

namespace Monica.Core.Execution.Models.Internal;

internal sealed class ExecutionBehaviorPlan
{
    private readonly Lazy<ExecutionBehaviorPlanContent> _content;
    private ExecutionBehaviorPlanContent? _materializedContent;
    private ExecutionPipelinePlanErrorSnapshot? _error;
    private DateTimeOffset? _completedAt;
    private int _status = (int)ExecutionPipelinePlanStatus.Building;

    public ExecutionBehaviorPlan(
        ExecutionDescriptor descriptor,
        Func<ExecutionBehaviorPlanContent> materialize)
    {
        Descriptor = ExecutionPipelineCatalogSnapshotFactory.CreateDescriptor(descriptor);
        PlanKey = ExecutionPipelinePlanKey.Create(descriptor);
        StartedAt = DateTimeOffset.UtcNow;
        _content = new Lazy<ExecutionBehaviorPlanContent>(
            () => Materialize(materialize),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string PlanKey { get; }

    public DateTimeOffset StartedAt { get; }

    public ExecutionDescriptorSnapshot Descriptor { get; }

    public Type[] BehaviorTypes => _content.Value.BehaviorTypes;

    public ExecutionPipelinePlanSnapshot Inspect()
    {
        try
        {
            _ = _content.Value;
        }
        catch (Exception)
        {
            // Inspection reports deterministic materialization failures through the snapshot. Execution still
            // observes the original exception cached by Lazy<T>.
        }

        return CreateSnapshot();
    }

    public ExecutionPipelinePlanSnapshot CreateSnapshot()
    {
        var status = (ExecutionPipelinePlanStatus)Volatile.Read(ref _status);
        var behaviors = status == ExecutionPipelinePlanStatus.Ready
            ? _materializedContent!.Behaviors
            : ImmutableArray<ExecutionPipelineAppliedBehaviorSnapshot>.Empty;

        return new ExecutionPipelinePlanSnapshot(
            PlanKey,
            status,
            StartedAt,
            status == ExecutionPipelinePlanStatus.Building ? null : _completedAt,
            Descriptor,
            behaviors,
            status == ExecutionPipelinePlanStatus.Faulted ? _error : null);
    }

    private ExecutionBehaviorPlanContent Materialize(Func<ExecutionBehaviorPlanContent> materialize)
    {
        try
        {
            var content = materialize();
            _materializedContent = content;
            _completedAt = DateTimeOffset.UtcNow;
            Volatile.Write(ref _status, (int)ExecutionPipelinePlanStatus.Ready);
            return content;
        }
        catch (Exception exception)
        {
            _error = new ExecutionPipelinePlanErrorSnapshot(
                exception.GetType().FullName ?? exception.GetType().Name,
                exception.GetMessageRecursively());
            _completedAt = DateTimeOffset.UtcNow;
            Volatile.Write(ref _status, (int)ExecutionPipelinePlanStatus.Faulted);
            throw;
        }
    }
}

internal sealed record ExecutionBehaviorPlanContent(
    Type[] BehaviorTypes,
    ImmutableArray<ExecutionPipelineAppliedBehaviorSnapshot> Behaviors);
