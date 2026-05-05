using System.Text.RegularExpressions;

namespace Monica.UI.Theming;

/// <summary>
/// Provides boundary conversions for <see cref="MonicaThemeKind"/>.
/// </summary>
public static partial class MonicaThemeKindExtensions
{
    /// <summary>
    /// Converts a theme kind to the kebab-case token used by CSS classes and the document <c>data-theme</c> attribute.
    /// </summary>
    /// <param name="themeKind">The theme kind to convert.</param>
    /// <returns>A deterministic kebab-case token such as <c>material-design-3</c>.</returns>
    public static string ToCssToken(this MonicaThemeKind themeKind)
    {
        var token = AcronymBoundaryRegex().Replace(themeKind.ToString(), "$1-$2");
        token = LowercaseBoundaryRegex().Replace(token, "$1-$2");
        token = DigitBoundaryRegex().Replace(token, "$1-$2");
        return token.ToLowerInvariant();
    }

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")]
    private static partial Regex AcronymBoundaryRegex();

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex LowercaseBoundaryRegex();

    [GeneratedRegex("([a-zA-Z])([0-9])")]
    private static partial Regex DigitBoundaryRegex();
}
