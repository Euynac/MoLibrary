namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Captures the effective host-level configuration used by one seeder run.
/// </summary>
public sealed record SeederConfigurationSnapshot
{
    /// <summary>Gets the maximum number of concurrently running seeders.</summary>
    public required int MaxConcurrency { get; init; }

    /// <summary>Gets the execution mode inherited by seeders without an override.</summary>
    public required SeederExecutionMode DefaultExecutionMode { get; init; }

    /// <summary>Gets the criticality inherited by seeders without an override.</summary>
    public required SeederCriticality DefaultCriticality { get; init; }

    /// <summary>Gets the maximum attempt count inherited by seeders without an override.</summary>
    public required int DefaultMaxAttempts { get; init; }

    /// <summary>Gets the initial retry delay.</summary>
    public required TimeSpan RetryBaseDelay { get; init; }

    /// <summary>Gets the upper bound for one retry delay.</summary>
    public required TimeSpan RetryMaxDelay { get; init; }

    /// <summary>Gets the failure behavior inherited by seeders without an override.</summary>
    public required SeederFailureBehavior DefaultFailureBehavior { get; init; }
}
