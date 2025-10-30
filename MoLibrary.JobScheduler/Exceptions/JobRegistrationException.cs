namespace MoLibrary.JobScheduler.Exceptions;

/// <summary>
/// Exception thrown when a job registration operation fails.
/// This typically occurs when attempting to register a job with a duplicate JobKey
/// or when job configuration validation fails during registration.
/// </summary>
public class JobRegistrationException : Exception
{
    /// <summary>
    /// Gets the job key that caused the registration failure.
    /// </summary>
    public string? JobKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRegistrationException"/> class.
    /// </summary>
    public JobRegistrationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRegistrationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public JobRegistrationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRegistrationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JobRegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRegistrationException"/> class with a specified error message
    /// and the job key that caused the failure.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="jobKey">The job key that caused the registration failure.</param>
    public JobRegistrationException(string message, string jobKey)
        : base(message)
    {
        JobKey = jobKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobRegistrationException"/> class with a specified error message,
    /// the job key that caused the failure, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="jobKey">The job key that caused the registration failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JobRegistrationException(string message, string jobKey, Exception innerException)
        : base(message, innerException)
    {
        JobKey = jobKey;
    }
}
