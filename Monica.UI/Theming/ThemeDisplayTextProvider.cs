using System.Globalization;
using Microsoft.Extensions.Localization;
using Monica.UI.Localization;

namespace Monica.UI.Theming;

public static class ThemeDisplayTextProvider
{
    public static IReadOnlyList<(MonicaThemeKind Kind, string DisplayName, string Description)> GetThemeOptions(IStringLocalizer<SharedResource> localizer)
    {
        return ThemeCatalog.GetAvailableThemes()
            .Select(theme =>
            {
                var displayName = GetLocalizedThemeValue(localizer, theme.Kind, "Name", CreateNameFallback(theme.Kind));
                var description = GetLocalizedThemeValue(localizer, theme.Kind, "Description", string.Empty);
                return (theme.Kind, displayName, description);
            })
            .ToArray();
    }

    public static string GetCurrentThemeSummary(
        IStringLocalizer<SharedResource> localizer,
        MonicaThemeKind themeKind,
        bool isDarkMode)
    {
        var displayName = GetLocalizedThemeValue(localizer, themeKind, "Name", CreateNameFallback(themeKind));
        var mode = isDarkMode ? localizer["Theme:Dark"] : localizer["Theme:Light"];
        return $"{displayName} - {mode}";
    }

    private static string GetLocalizedThemeValue(
        IStringLocalizer<SharedResource> localizer,
        MonicaThemeKind themeKind,
        string suffix,
        string fallback)
    {
        var key = $"Theme:Options:{themeKind}:{suffix}";
        var localized = localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }

    private static string CreateNameFallback(MonicaThemeKind themeKind)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(themeKind.ToCssToken().Replace('-', ' '));
    }
}
