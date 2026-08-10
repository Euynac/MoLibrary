using System.Collections.Immutable;

namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Captures the point-in-time execution state of one seeder.
/// </summary>
public sealed record SeederExecutionSnapshot
{
    /// <summary>Gets the full seeder type name used as its stable runtime identity.</summary>
    public required string SeederTypeName { get; init; }

    /// <summary>Gets the resolved execution mode after host defaults have been applied.</summary>
    public required SeederExecutionMode ExecutionMode { get; init; }

    /// <summary>Gets the resolved readiness criticality after host defaults have been applied.</summary>
    public required SeederCriticality Criticality { get; init; }

    /// <summary>Gets the current execution status.</summary>
    public required SeederStatus Status { get; init; }

    /// <summary>Gets the number of attempts that have started.</summary>
    public required int Attempts { get; init; }

    /// <summary>Gets the maximum attempts allowed for this seeder.</summary>
    public required int MaxAttempts { get; init; }

    /// <summary>Gets the stable type names of the seeder's direct dependencies.</summary>
    public ImmutableArray<string> Dependencies { get; init; } = [];

    /// <summary>Gets when the first attempt started, or <see langword="null"/> while pending.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }

    /// <summary>Gets when the seeder reached a terminal state, or <see langword="null"/> while pending or running.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Gets the final exception type or blocking reason category, when available.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Gets the final exception or blocking message, when available.</summary>
    public string? ErrorMessage { get; init; }
}
