namespace Monica.JobScheduler.Models;

/// <summary>
/// Provides execution context for job execution.
/// Contains all necessary information for both recurring and triggered jobs.
/// </summary>
public class JobExecutionContext
{
    /// <summary>
    /// Gets or sets the service provider for resolving job instances and dependencies.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// Gets or sets the CLR type of the job to execute.
    /// </summary>
    public required Type JobType { get; init; }

    /// <summary>
    /// Gets or sets the job arguments (for triggered jobs only).
    /// Null for recurring jobs.
    /// </summary>
    public object? JobArgs { get; init; }

    /// <summary>
    /// Gets or sets the cancellation token for the job execution.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }
}