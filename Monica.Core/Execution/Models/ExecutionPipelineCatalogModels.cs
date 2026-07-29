using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Execution.Models;

/// <summary>
/// Provides stable and display-oriented identities for a CLR type exposed by execution-pipeline diagnostics.
/// </summary>
/// <param name="Identity">
/// The assembly-qualified identity used to distinguish types from different assemblies and versions.
/// </param>
/// <param name="DisplayName">The human-readable fully qualified type name.</param>
public sealed record ExecutionTypeSnapshot(string Identity, string DisplayName);

/// <summary>
/// Describes one behavior registration owned by the current Monica host.
/// </summary>
/// <param name="BehaviorType">The registered behavior implementation type.</param>
/// <param name="Order">The configured outer-to-inner behavior order.</param>
/// <param name="Lifetime">The dependency-injection lifetime used for behavior instances.</param>
/// <param name="SourceModuleKey">
/// The module that contributed the behavior, or <see langword="null"/> when the host registered it directly.
/// </param>
/// <param name="IsOpenGeneric">Whether the registered implementation is an open generic behavior.</param>
/// <param name="HasDescriptorFilter">Whether the registration applies a descriptor filter.</param>
public sealed record ExecutionBehaviorRegistrationSnapshot(
    ExecutionTypeSnapshot BehaviorType,
    int Order,
    ServiceLifetime Lifetime,
    ModuleKey? SourceModuleKey,
    bool IsOpenGeneric,
    bool HasDescriptorFilter);

/// <summary>
/// Defines the materialization state of a cached execution-pipeline plan.
/// </summary>
public enum ExecutionPipelinePlanStatus
{
    /// <summary>
    /// The descriptor filters and generic behavior contracts are currently being evaluated.
    /// </summary>
    Building,

    /// <summary>
    /// The immutable applied-behavior chain is available.
    /// </summary>
    Ready,

    /// <summary>
    /// Plan materialization failed and the deterministic failure is cached for the host lifetime.
    /// </summary>
    Faulted
}

/// <summary>
/// Captures the stable, invocation-independent metadata used to select an execution plan.
/// </summary>
/// <param name="OperationKey">The stable aggregate identity of the operation.</param>
/// <param name="DisplayName">The human-readable operation name.</param>
/// <param name="Point">The subsystem execution point.</param>
/// <param name="ComponentType">The concrete or contractual component type being invoked.</param>
/// <param name="ContractType">The adapter contract used to identify the entry method, when present.</param>
/// <param name="EntryMethod">The concrete entry method identity, when present.</param>
/// <param name="InputType">The pipeline input type.</param>
/// <param name="ResultType">The pipeline result type.</param>
/// <param name="IsBusinessOperation">Whether the execution represents application business work.</param>
/// <param name="TransactionMode">The automatic transaction policy for the boundary.</param>
public sealed record ExecutionDescriptorSnapshot(
    string OperationKey,
    string DisplayName,
    ExecutionPoint Point,
    ExecutionTypeSnapshot ComponentType,
    ExecutionTypeSnapshot? ContractType,
    string? EntryMethod,
    ExecutionTypeSnapshot InputType,
    ExecutionTypeSnapshot ResultType,
    bool IsBusinessOperation,
    ExecutionTransactionMode TransactionMode);

/// <summary>
/// Describes one behavior in the exact outer-to-inner order applied to an observed execution plan.
/// </summary>
/// <param name="Position">The one-based outer-to-inner position.</param>
/// <param name="RegisteredType">The behavior type originally registered with the host.</param>
/// <param name="ResolvedType">The closed behavior type resolved for this descriptor.</param>
/// <param name="Order">The configured behavior order.</param>
/// <param name="Lifetime">The dependency-injection lifetime used for behavior instances.</param>
/// <param name="SourceModuleKey">
/// The module that contributed the behavior, or <see langword="null"/> when the host registered it directly.
/// </param>
public sealed record ExecutionPipelineAppliedBehaviorSnapshot(
    int Position,
    ExecutionTypeSnapshot RegisteredType,
    ExecutionTypeSnapshot ResolvedType,
    int Order,
    ServiceLifetime Lifetime,
    ModuleKey? SourceModuleKey);

/// <summary>
/// Describes a deterministic failure encountered while materializing an execution plan.
/// </summary>
/// <param name="ExceptionType">The fully qualified exception type.</param>
/// <param name="Message">The recursive exception message suitable for diagnostics.</param>
public sealed record ExecutionPipelinePlanErrorSnapshot(string ExceptionType, string Message);

/// <summary>
/// Describes one cached execution plan and the exact behavior chain used by the pipeline.
/// </summary>
/// <param name="PlanKey">
/// The stable plan identity, including operation identity and plan-affecting business and transaction policies.
/// </param>
/// <param name="Status">The current plan materialization state.</param>
/// <param name="StartedAt">The time plan materialization began.</param>
/// <param name="CompletedAt">The completion time for a ready or faulted plan.</param>
/// <param name="Descriptor">The invocation-independent descriptor metadata.</param>
/// <param name="Behaviors">The immutable outer-to-inner behavior chain.</param>
/// <param name="Error">The cached materialization failure, when the plan is faulted.</param>
public sealed record ExecutionPipelinePlanSnapshot(
    string PlanKey,
    ExecutionPipelinePlanStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    ExecutionDescriptorSnapshot Descriptor,
    ImmutableArray<ExecutionPipelineAppliedBehaviorSnapshot> Behaviors,
    ExecutionPipelinePlanErrorSnapshot? Error);

/// <summary>
/// Represents a point-in-time view of behavior registrations and observed execution plans for one host.
/// </summary>
/// <param name="CapturedAt">The time the snapshot was created.</param>
/// <param name="Registrations">All behavior registrations in deterministic execution order.</param>
/// <param name="Plans">
/// Plans that have been observed through execution or explicitly materialized through the catalog.
/// </param>
public sealed record ExecutionPipelineCatalogSnapshot(
    DateTimeOffset CapturedAt,
    ImmutableArray<ExecutionBehaviorRegistrationSnapshot> Registrations,
    ImmutableArray<ExecutionPipelinePlanSnapshot> Plans);
