using System.Collections.Immutable;

namespace Monica.Framework.Seeder.Models.Internal;

internal sealed record SeederDescriptor(
    Type SeederType,
    string SeederTypeName,
    SeederExecutionMode ExecutionMode,
    SeederCriticality Criticality,
    int MaxAttempts,
    ImmutableArray<Type> Dependencies);
