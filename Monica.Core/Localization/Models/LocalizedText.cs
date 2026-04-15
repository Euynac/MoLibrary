using Microsoft.Extensions.Localization;

namespace Monica.Core.Localization.Models;

/// <summary>
/// Represents text that can be resolved through a localization key while preserving a fallback literal.
/// </summary>
public sealed record LocalizedText
{
    /// <summary>
    /// Gets the fallback literal used when no localized value is available.
    /// </summary>
    public required string Fallback { get; init; }

    /// <summary>
    /// Gets the optional localization key used to resolve the final text.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Resolves the final text using the provided localizer.
    /// </summary>
    /// <param name="localizer">The localizer used to resolve <see cref="Key"/>.</param>
    /// <returns>The localized value when available; otherwise <see cref="Fallback"/>.</returns>
    public string Resolve(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        if (string.IsNullOrWhiteSpace(Key))
        {
            return Fallback;
        }

        var localized = localizer[Key];
        return localized.ResourceNotFound ? Fallback : localized.Value;
    }

    /// <summary>
    /// Creates a non-localized text value that always resolves to the provided literal.
    /// </summary>
    /// <param name="fallback">The literal text.</param>
    /// <returns>A plain text instance.</returns>
    public static LocalizedText Plain(string fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        return new LocalizedText
        {
            Fallback = fallback
        };
    }

    /// <summary>
    /// Creates a localized text value with a fallback literal.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="fallback">The fallback literal used when the key is missing.</param>
    /// <returns>A localized text instance.</returns>
    public static LocalizedText Resource(string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(fallback);

        return new LocalizedText
        {
            Key = key,
            Fallback = fallback
        };
    }

    /// <summary>
    /// Creates a plain text instance when the input is not null; otherwise returns null.
    /// </summary>
    /// <param name="fallback">The optional literal text.</param>
    /// <returns>A plain text instance or null.</returns>
    public static LocalizedText? PlainOrNull(string? fallback)
    {
        return fallback is null ? null : Plain(fallback);
    }
}
