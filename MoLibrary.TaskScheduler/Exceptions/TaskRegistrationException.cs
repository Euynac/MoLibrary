namespace MoLibrary.TaskScheduler.Exceptions;

/// <summary>
/// Exception thrown when a task registration operation fails.
/// This typically occurs when attempting to register a task with a duplicate TaskKey
/// or when task configuration validation fails during registration.
/// </summary>
public class TaskRegistrationException : Exception
{
    /// <summary>
    /// Gets the task key that caused the registration failure.
    /// </summary>
    public string? TaskKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRegistrationException"/> class.
    /// </summary>
    public TaskRegistrationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRegistrationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TaskRegistrationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRegistrationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TaskRegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRegistrationException"/> class with a specified error message
    /// and the task key that caused the failure.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="taskKey">The task key that caused the registration failure.</param>
    public TaskRegistrationException(string message, string taskKey)
        : base(message)
    {
        TaskKey = taskKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskRegistrationException"/> class with a specified error message,
    /// the task key that caused the failure, and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="taskKey">The task key that caused the registration failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public TaskRegistrationException(string message, string taskKey, Exception innerException)
        : base(message, innerException)
    {
        TaskKey = taskKey;
    }
}
