namespace Monica.Core.ExceptionHandling.Exceptions;

/// <summary>
/// Exception wrapper that attaches extra contextual information to another exception.
/// The handler unwraps this type automatically, merges its metadata into response <c>ExtraInfo</c>,
/// and continues processing the inner exception.
/// </summary>
public class ContextualException : Exception
{
    /// <summary>
    /// Gets the extra contextual values stored on the wrapper.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ExtraInfo => _extraInfo;
    private readonly Dictionary<string, object?> _extraInfo = new();

    /// <summary>
    /// Initializes a new wrapper exception.
    /// </summary>
    /// <param name="message">The wrapper message.</param>
    /// <param name="innerException">The actual exception being wrapped.</param>
    public ContextualException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new wrapper exception and falls back to the inner exception message when needed.
    /// </summary>
    /// <param name="innerException">The actual exception being wrapped.</param>
    /// <param name="message">An optional wrapper message. When omitted, the inner exception message is used.</param>
    public ContextualException(Exception innerException, string? message = null)
        : base(message ?? innerException.Message, innerException)
    {
    }

    /// <summary>
    /// Adds one extra info entry and returns the current wrapper for fluent chaining.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The current instance.</returns>
    public ContextualException WithExtraInfo(string key, object? value)
    {
        _extraInfo[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple extra info entries and returns the current wrapper for fluent chaining.
    /// </summary>
    /// <param name="extraInfo">The metadata entries to add.</param>
    /// <returns>The current instance.</returns>
    public ContextualException WithExtraInfo(IDictionary<string, object?> extraInfo)
    {
        foreach (var kvp in extraInfo)
        {
            _extraInfo[kvp.Key] = kvp.Value;
        }
        return this;
    }
}
