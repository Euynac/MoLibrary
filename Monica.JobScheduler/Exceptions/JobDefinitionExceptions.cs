namespace Monica.JobScheduler.Exceptions;

/// <summary>
/// Thrown when a job definition targeted by an operation does not exist or is not currently present.
/// </summary>
public sealed class JobDefinitionNotFoundException(string message) : InvalidOperationException(message);

/// <summary>
/// Thrown when a policy update loses optimistic concurrency against a concurrent editor.
/// </summary>
public sealed class JobPolicyConcurrencyException(string jobKey)
    : InvalidOperationException(
        $"The policy for job '{jobKey}' changed before this update was applied.");
