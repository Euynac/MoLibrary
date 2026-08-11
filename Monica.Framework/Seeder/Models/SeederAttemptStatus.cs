namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Describes the lifecycle state of one seeder attempt.
/// </summary>
public enum SeederAttemptStatus
{
    /// <summary>The attempt is currently executing.</summary>
    Running,

    /// <summary>The attempt completed successfully.</summary>
    Succeeded,

    /// <summary>The attempt ended with a non-cancellation exception.</summary>
    Failed,

    /// <summary>The attempt observed cancellation.</summary>
    Cancelled
}
