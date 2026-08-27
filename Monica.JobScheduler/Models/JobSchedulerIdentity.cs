namespace Monica.JobScheduler.Models;

/// <summary>
/// Centralizes persistence-safe scheduler identity limits shared by every provider.
/// </summary>
public static class JobSchedulerIdentity
{
    /// <summary>
    /// Maximum length of scope, owner, worker, and execution identities.
    /// </summary>
    public const int STANDARD_MAX_LENGTH = 128;

    /// <summary>
    /// Maximum length of an owner-scoped logical job key.
    /// </summary>
    public const int JOB_KEY_MAX_LENGTH = 256;

    /// <summary>
    /// Validates a standard persisted scheduler identity.
    /// </summary>
    public static void ValidateStandard(string value, string parameterName) =>
        Validate(value, STANDARD_MAX_LENGTH, parameterName);

    /// <summary>
    /// Validates a logical job key.
    /// </summary>
    public static void ValidateJobKey(string value, string parameterName) =>
        Validate(value, JOB_KEY_MAX_LENGTH, parameterName);

    private static void Validate(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A scheduler identity cannot be empty.", parameterName);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Scheduler identity '{parameterName}' cannot exceed {maxLength} characters.",
                parameterName);
        }
    }
}
