namespace MoLibrary.JobScheduler.Exceptions;

public class TriggeredJobExecutionException : Exception
{
    public TriggeredJobExecutionException()
    {
    }

    /// <summary>
    ///     Creates a new <see cref="TriggeredJobExecutionException" /> object.
    /// </summary>
    /// <param name="message">Exception message</param>
    /// <param name="innerException">Inner exception</param>
    public TriggeredJobExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public required object? JobArgs { get; set; } 
    public required string JobType { get; set; } 
}