namespace Monica.Core.Execution;

/// <summary>
/// Identifies a stable execution boundary understood by execution-pipeline adapters.
/// </summary>
/// <remarks>
/// Module-specific adapters should publish reusable constants rather than constructing points for every invocation.
/// Point values are case-sensitive and must not contain leading or trailing whitespace.
/// </remarks>
public sealed record ExecutionPoint
{
    /// <summary>
    /// Initializes an execution point.
    /// </summary>
    /// <param name="value">The stable, non-empty point value.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is empty, contains only whitespace, or has surrounding whitespace.
    /// </exception>
    public ExecutionPoint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Execution point values must not contain leading or trailing whitespace.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the stable, case-sensitive point value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
