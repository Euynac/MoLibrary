namespace Monica.Configuration.Models;

/// <summary>
/// Base type for validation rules discovered from standard .NET validation metadata.
/// </summary>
public abstract record ConfigurationValidationRule
{
    /// <summary>
    /// Gets the validation error message, when one was supplied by the source metadata.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Requires a numeric value to be within the provided range.
/// </summary>
/// <param name="Min">The inclusive minimum value.</param>
/// <param name="Max">The inclusive maximum value.</param>
public sealed record RangeRule(decimal? Min, decimal? Max) : ConfigurationValidationRule;

/// <summary>
/// Requires a string value to match a regular expression pattern.
/// </summary>
/// <param name="Pattern">The regular expression pattern.</param>
public sealed record RegexRule(string Pattern) : ConfigurationValidationRule;

/// <summary>
/// Requires a value to be one of the allowed values.
/// </summary>
/// <param name="Values">The allowed values.</param>
public sealed record AllowedValuesRule(IReadOnlyList<string> Values) : ConfigurationValidationRule;

/// <summary>
/// Requires a string or collection to not exceed a maximum length.
/// </summary>
/// <param name="Max">The maximum length.</param>
public sealed record MaxLengthRule(int Max) : ConfigurationValidationRule;

/// <summary>
/// Requires a string or collection to meet a minimum length.
/// </summary>
/// <param name="Min">The minimum length.</param>
public sealed record MinLengthRule(int Min) : ConfigurationValidationRule;

/// <summary>
/// Requires a value to be present.
/// </summary>
public sealed record RequiredRule : ConfigurationValidationRule;
