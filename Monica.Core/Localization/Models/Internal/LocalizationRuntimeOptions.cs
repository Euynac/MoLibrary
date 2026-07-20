using System.Globalization;

namespace Monica.Core.Localization.Models.Internal;

internal sealed record LocalizationRuntimeOptions(
    string DefaultCulture,
    IReadOnlyList<string> SupportedCultures)
{
    public static LocalizationRuntimeOptions Create(
        string defaultCulture,
        IEnumerable<string> supportedCultures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCulture);
        ArgumentNullException.ThrowIfNull(supportedCultures);

        var normalizedDefaultCulture = NormalizeCulture(defaultCulture);
        var normalizedSupportedCultures = supportedCultures
            .Select(NormalizeCulture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!normalizedSupportedCultures.Contains(normalizedDefaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            normalizedSupportedCultures.Add(normalizedDefaultCulture);
        }

        return new LocalizationRuntimeOptions(normalizedDefaultCulture, normalizedSupportedCultures);
    }

    private static string NormalizeCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        return CultureInfo.GetCultureInfo(cultureName.Trim()).Name;
    }
}
