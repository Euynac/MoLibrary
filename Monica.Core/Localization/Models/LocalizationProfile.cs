using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Monica.Core.Localization.Models;

/// <summary>
/// Describes the validated, immutable localization contract for one Monica host.
/// </summary>
/// <remarks>
/// The profile is created from <see cref="Monica.Modules.ModuleLocalizationOption"/> during module composition and is
/// registered as a host-owned singleton. Runtime services should consume this profile instead of the mutable module
/// options so every localization path observes the same canonical culture names and display labels.
/// </remarks>
public sealed class LocalizationProfile
{
    private static readonly char[] INVALID_COOKIE_NAME_CHARACTERS =
        ['(', ')', '<', '>', '@', ',', ';', ':', '\\', '"', '/', '[', ']', '?', '=', '{', '}'];

    private readonly FrozenDictionary<string, string> _supportedCultureLookup;

    private LocalizationProfile(
        string defaultCulture,
        IReadOnlyList<string> supportedCultures,
        FrozenDictionary<string, string> cultureDisplayNames,
        string cookieName)
    {
        DefaultCulture = defaultCulture;
        SupportedCultures = supportedCultures;
        CultureDisplayNames = cultureDisplayNames;
        CookieName = cookieName;
        _supportedCultureLookup = supportedCultures.ToFrozenDictionary(
            static culture => culture,
            static culture => culture,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the canonical fallback culture. This value is always present in <see cref="SupportedCultures"/>.
    /// </summary>
    public string DefaultCulture { get; }

    /// <summary>
    /// Gets the canonical, ordered cultures supported by the host.
    /// </summary>
    public IReadOnlyList<string> SupportedCultures { get; }

    /// <summary>
    /// Gets the frozen display-name map for every supported culture.
    /// </summary>
    public IReadOnlyDictionary<string, string> CultureDisplayNames { get; }

    /// <summary>
    /// Gets the validated cookie name used by ASP.NET Core request localization.
    /// </summary>
    public string CookieName { get; }

    /// <summary>
    /// Creates a validated profile and canonicalizes all culture identifiers through <see cref="CultureInfo"/>.
    /// </summary>
    /// <param name="defaultCulture">The required fallback culture.</param>
    /// <param name="supportedCultures">The non-empty, duplicate-free ordered set of supported cultures.</param>
    /// <param name="cultureDisplayNames">
    /// Optional display names keyed by supported culture. Missing names fall back to
    /// <see cref="CultureInfo.NativeName"/>.
    /// </param>
    /// <param name="cookieName">The RFC token-compatible request-culture cookie name.</param>
    /// <returns>An immutable host localization profile.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a culture is invalid, duplicated, unsupported by the default or display-name configuration, or the
    /// cookie name is not a valid token.
    /// </exception>
    public static LocalizationProfile Create(
        string defaultCulture,
        IEnumerable<string> supportedCultures,
        IReadOnlyDictionary<string, string>? cultureDisplayNames,
        string cookieName)
    {
        ArgumentNullException.ThrowIfNull(supportedCultures);

        var normalizedDefaultCulture = NormalizeCulture(defaultCulture, nameof(defaultCulture));
        var normalizedSupportedCultures = new List<string>();
        var seenCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in supportedCultures)
        {
            var normalizedCulture = NormalizeCulture(culture, nameof(supportedCultures));
            if (!seenCultures.Add(normalizedCulture))
            {
                throw new ArgumentException(
                    $"Supported culture '{normalizedCulture}' is configured more than once.",
                    nameof(supportedCultures));
            }

            normalizedSupportedCultures.Add(normalizedCulture);
        }

        if (normalizedSupportedCultures.Count == 0)
        {
            throw new ArgumentException("At least one supported culture must be configured.", nameof(supportedCultures));
        }

        if (!seenCultures.Contains(normalizedDefaultCulture))
        {
            throw new ArgumentException(
                $"Default culture '{normalizedDefaultCulture}' must be included in the supported cultures.",
                nameof(defaultCulture));
        }

        var displayNames = BuildDisplayNames(normalizedSupportedCultures, cultureDisplayNames);
        var normalizedCookieName = ValidateCookieName(cookieName);

        return new LocalizationProfile(
            normalizedDefaultCulture,
            new ReadOnlyCollection<string>(normalizedSupportedCultures),
            displayNames,
            normalizedCookieName);
    }

    /// <summary>
    /// Resolves a culture candidate to its canonical configured value.
    /// </summary>
    /// <param name="candidate">A culture name in any casing or an alias understood by <see cref="CultureInfo"/>.</param>
    /// <param name="culture">The canonical configured culture when resolution succeeds.</param>
    /// <returns><see langword="true"/> when the candidate is valid and supported.</returns>
    public bool TryResolveSupportedCulture(string? candidate, out string culture)
    {
        culture = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            var normalized = CultureInfo.GetCultureInfo(candidate.Trim()).Name;
            return _supportedCultureLookup.TryGetValue(normalized, out culture!);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the first supported candidate, falling back to <see cref="DefaultCulture"/>.
    /// </summary>
    /// <param name="candidates">Culture candidates ordered from most to least preferred.</param>
    /// <returns>The canonical selected culture.</returns>
    public string ResolveCulture(params string?[] candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (var candidate in candidates)
        {
            if (TryResolveSupportedCulture(candidate, out var culture))
            {
                return culture;
            }
        }

        return DefaultCulture;
    }

    /// <summary>
    /// Gets the configured display name for a supported culture.
    /// </summary>
    /// <param name="culture">The culture to resolve.</param>
    /// <returns>The configured or native display name.</returns>
    /// <exception cref="ArgumentException">Thrown when the culture is invalid or unsupported.</exception>
    public string GetDisplayName(string culture)
    {
        if (!TryResolveSupportedCulture(culture, out var supportedCulture))
        {
            throw new ArgumentException($"Culture '{culture}' is not supported by this host.", nameof(culture));
        }

        return CultureDisplayNames[supportedCulture];
    }

    private static FrozenDictionary<string, string> BuildDisplayNames(
        IReadOnlyList<string> supportedCultures,
        IReadOnlyDictionary<string, string>? configuredDisplayNames)
    {
        var normalizedConfiguredNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (configuredDisplayNames is not null)
        {
            foreach (var (culture, displayName) in configuredDisplayNames)
            {
                var normalizedCulture = NormalizeCulture(culture, nameof(configuredDisplayNames));
                if (!supportedCultures.Contains(normalizedCulture, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"Display name culture '{normalizedCulture}' is not included in the supported cultures.",
                        nameof(configuredDisplayNames));
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    throw new ArgumentException(
                        $"Display name for culture '{normalizedCulture}' must not be empty.",
                        nameof(configuredDisplayNames));
                }

                if (!normalizedConfiguredNames.TryAdd(normalizedCulture, displayName.Trim()))
                {
                    throw new ArgumentException(
                        $"Display name culture '{normalizedCulture}' is configured more than once.",
                        nameof(configuredDisplayNames));
                }
            }
        }

        return supportedCultures.ToFrozenDictionary(
            static culture => culture,
            culture => normalizedConfiguredNames.TryGetValue(culture, out var displayName)
                ? displayName
                : CultureInfo.GetCultureInfo(culture).NativeName,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeCulture(string culture, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture, parameterName);

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException exception)
        {
            throw new ArgumentException($"Culture '{culture}' is not valid.", parameterName, exception);
        }
    }

    private static string ValidateCookieName(string cookieName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);
        var normalized = cookieName.Trim();
        if (normalized.Any(character =>
                character <= 0x20
                || character >= 0x7f
                || INVALID_COOKIE_NAME_CHARACTERS.Contains(character)))
        {
            throw new ArgumentException(
                $"Cookie name '{cookieName}' contains characters that are not valid in an HTTP token.",
                nameof(cookieName));
        }

        return normalized;
    }
}
