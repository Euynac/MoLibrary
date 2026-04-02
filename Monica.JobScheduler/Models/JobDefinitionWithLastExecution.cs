namespace Monica.JobScheduler.Models;

/// <summary>
/// Job definition with last execution information
/// </summary>
public class JobDefinitionWithLastExecution
{
    /// <summary>
    /// job definition
    /// </summary>
    public required JobDefinition Definition { get; set; }

    /// <summary>
    /// The last executed instance (if any)
    /// </summary>
    public JobInstance? LastExecution { get; set; }

    /// <summary>
    /// Next execution time (only for RecurringJob)
    /// </summary>
    public DateTime? NextExecutionTime { get; set; }
}
