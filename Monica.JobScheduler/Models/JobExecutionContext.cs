using Microsoft.Extensions.Logging;

namespace Monica.JobScheduler.Models;

/// <summary>
/// Provides execution context for job execution.
/// Contains all necessary information for both recurring and triggered jobs.
/// </summary>
public class JobExecutionContext
{
    /// <summary>
    /// Gets the durable execution identifier being executed.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the scoped service provider for resolving the job and its dependencies.
    /// </summary>
    public required IServiceProvider ServiceProvider { get; init; }

    /// <summary>
    /// Gets the CLR type of the job to execute.
    /// </summary>
    public required Type JobType { get; init; }

    /// <summary>
    /// Gets the job arguments (for triggered jobs only).
    /// Null for recurring jobs.
    /// </summary>
    public object? JobArgs { get; init; }

    /// <summary>
    /// Gets the cancellation token for the job execution.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the fenced durable log sink for the current execution lease.
    /// </summary>
    internal Func<string, LogLevel, Exception?, CancellationToken, Task> WriteExecutionLogAsync { get; init; } =
        static (_, _, _, _) => Task.CompletedTask;
}
