namespace Monica.StateStore.Exceptions;

/// <summary>
/// ETag error types for state store operations
/// </summary>
public enum ETagErrorType
{
    /// <summary>
    /// ETag format is invalid (e.g., non-numeric string when numeric expected)
    /// </summary>
    Invalid,

    /// <summary>
    /// ETag does not match current version (concurrent modification conflict)
    /// </summary>
    Mismatch
}

/// <summary>
/// Exception thrown when ETag validation fails
/// </summary>
public class ETagException : Exception
{
    /// <summary>
    /// The type of ETag error
    /// </summary>
    public ETagErrorType ErrorType { get; }

    /// <summary>
    /// The key that caused the error
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The expected ETag value (if applicable)
    /// </summary>
    public string? ExpectedETag { get; }

    /// <summary>
    /// The actual ETag value found (if applicable)
    /// </summary>
    public string? ActualETag { get; }

    public ETagException(ETagErrorType errorType, string key,
        string? expectedETag = null, string? actualETag = null)
        : base(BuildMessage(errorType, key, expectedETag, actualETag))
    {
        ErrorType = errorType;
        Key = key;
        ExpectedETag = expectedETag;
        ActualETag = actualETag;
    }

    public ETagException(ETagErrorType errorType, string key,
        string? expectedETag, string? actualETag, Exception innerException)
        : base(BuildMessage(errorType, key, expectedETag, actualETag), innerException)
    {
        ErrorType = errorType;
        Key = key;
        ExpectedETag = expectedETag;
        ActualETag = actualETag;
    }

    private static string BuildMessage(ETagErrorType type, string key,
        string? expected, string? actual) => type switch
    {
        ETagErrorType.Invalid => $"Invalid ETag format for key '{key}': '{expected}'",
        ETagErrorType.Mismatch => $"ETag mismatch for key '{key}'. Expected: '{expected}', Actual: '{actual}'",
        _ => $"ETag error for key '{key}'"
    };
}
