using System.Globalization;
using Microsoft.Extensions.Localization;
using Monica.UI.Localization;

namespace Monica.UI.Theming;

public static class ThemeDisplayTextProvider
{
    public static IReadOnlyList<(string Name, string DisplayName, string Description)> GetThemeOptions(IStringLocalizer<SharedResource> localizer)
    {
        return ThemeCatalog.GetAvailableThemes()
            .Select(theme =>
            {
                var displayName = GetLocalizedThemeValue(localizer, theme.Name, "Name", CreateNameFallback(theme.Name));
                var description = GetLocalizedThemeValue(localizer, theme.Name, "Description", string.Empty);
                return (theme.Name, displayName, description);
            })
            .ToArray();
    }

    public static string GetCurrentThemeSummary(IStringLocalizer<SharedResource> localizer, string themeName, bool isDarkMode)
    {
        var displayName = GetLocalizedThemeValue(localizer, themeName, "Name", CreateNameFallback(themeName));
        var mode = isDarkMode ? localizer["Theme:Dark"] : localizer["Theme:Light"];
        return $"{displayName} - {mode}";
    }

    private static string GetLocalizedThemeValue(IStringLocalizer<SharedResource> localizer, string themeName, string suffix, string fallback)
    {
        var key = $"Theme:Options:{themeName}:{suffix}";
        var localized = localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }

    private static string CreateNameFallback(string themeName)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(themeName.Replace('-', ' '));
    }
}
