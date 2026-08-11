using System.Collections.Immutable;

namespace Monica.Framework.Seeder.Models.Internal;

internal sealed record SeederDescriptor(
    Type SeederType,
    string SeederName,
    string SeederTypeName,
    SeederExecutionMode ExecutionMode,
    SeederPolicySource ExecutionModeSource,
    SeederCriticality Criticality,
    SeederPolicySource CriticalitySource,
    SeederFailureBehavior FailureBehavior,
    SeederPolicySource FailureBehaviorSource,
    int MaxAttempts,
    SeederPolicySource MaxAttemptsSource,
    ImmutableArray<Type> Dependencies);
