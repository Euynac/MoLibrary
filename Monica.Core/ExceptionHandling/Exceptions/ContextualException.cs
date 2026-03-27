namespace Monica.Core.ExceptionHandling.Exceptions;

/// <summary>
/// Exception wrapper that attaches extra contextual information to another exception.
/// The handler unwraps this type automatically, merges its metadata into response <c>Metadata</c>,
/// and continues processing the inner exception.
/// </summary>
public class ContextualException : Exception
{
    /// <summary>
    /// Gets the extra contextual values stored on the wrapper.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata => _metadata;
    private readonly Dictionary<string, object?> _metadata = new();

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
    /// Adds one metadata entry and returns the current wrapper for fluent chaining.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The current instance.</returns>
    public ContextualException WithMetadata(string key, object? value)
    {
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries and returns the current wrapper for fluent chaining.
    /// </summary>
    /// <param name="metadata">The metadata entries to add.</param>
    /// <returns>The current instance.</returns>
    public ContextualException WithMetadata(IDictionary<string, object?> metadata)
    {
        foreach (var kvp in metadata)
        {
            _metadata[kvp.Key] = kvp.Value;
        }
        return this;
    }
}
