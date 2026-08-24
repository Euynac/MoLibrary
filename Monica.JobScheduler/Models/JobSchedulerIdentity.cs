namespace Monica.JobScheduler.Models;

/// <summary>
/// Centralizes persistence-safe scheduler identity limits shared by every provider.
/// </summary>
public static class JobSchedulerIdentity
{
    /// <summary>
    /// Maximum length of scope, release, owner, worker, and execution identities.
    /// </summary>
    public const int STANDARD_MAX_LENGTH = 128;

    /// <summary>
    /// Maximum length of a scope-wide logical job key. Owner and revision identities do not qualify this key.
    /// </summary>
    public const int JOB_KEY_MAX_LENGTH = 256;

    /// <summary>
    /// Exact length of scheduler SHA-256 hexadecimal hashes.
    /// </summary>
    public const int HASH_LENGTH = 64;

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

    /// <summary>
    /// Validates a scheduler hash identity.
    /// </summary>
    public static void ValidateHash(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != HASH_LENGTH
            || value.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                $"Scheduler hash '{parameterName}' must contain exactly {HASH_LENGTH} hexadecimal characters.",
                parameterName);
        }
    }

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
