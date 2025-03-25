namespace MoLibrary.BackgroundJob.Abstract.Jobs;

public class BackgroundJobExecutionException : Exception
{
    public BackgroundJobExecutionException()
    {
    }

    /// <summary>
    ///     Creates a new <see cref="BackgroundJobExecutionException" /> object.
    /// </summary>
    /// <param name="message">Exception message</param>
    /// <param name="innerException">Inner exception</param>
    public BackgroundJobExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public object JobArgs { get; set; } = default!;
    public string JobType { get; set; } = default!;
}