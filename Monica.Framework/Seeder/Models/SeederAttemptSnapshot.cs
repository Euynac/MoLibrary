namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Captures the point-in-time state of one seeder attempt.
/// </summary>
public sealed record SeederAttemptSnapshot
{
    /// <summary>Gets the one-based attempt number.</summary>
    public required int AttemptNumber { get; init; }

    /// <summary>Gets the current or terminal attempt status.</summary>
    public required SeederAttemptStatus Status { get; init; }

    /// <summary>Gets when the attempt started.</summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>Gets when the attempt completed, or <see langword="null"/> while it is running.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Gets the elapsed attempt duration at snapshot capture time.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>Gets the bounded exception type for an unsuccessful attempt.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Gets the bounded recursive exception message for an unsuccessful attempt.</summary>
    public string? ErrorMessage { get; init; }
}
