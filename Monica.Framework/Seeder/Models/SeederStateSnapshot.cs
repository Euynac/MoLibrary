using System.Collections.Immutable;

namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Provides an immutable point-in-time view of the host's complete seeder run.
/// </summary>
public sealed record SeederStateSnapshot
{
    /// <summary>Gets when this snapshot was captured.</summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>Gets when DAG scheduling began after application startup, or <see langword="null"/> beforehand.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }

    /// <summary>Gets when every seeder reached a terminal state, or <see langword="null"/> while work remains.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Gets the discovered seeders ordered by their full type names.</summary>
    public ImmutableArray<SeederExecutionSnapshot> Seeders { get; init; } = [];

    /// <summary>Gets whether the scheduler has completed or been cancelled.</summary>
    public bool IsCompleted => CompletedAtUtc is not null;
}
