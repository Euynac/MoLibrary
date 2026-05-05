namespace Monica.UI.Theming;

/// <summary>
/// Identifies the built-in Monica UI themes that can be selected by shell configuration or by the browser user.
/// </summary>
public enum MonicaThemeKind
{
    /// <summary>
    /// The default Monica theme with a modern SaaS visual style.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The baseline MudBlazor theme without Monica-specific visual styling.
    /// </summary>
    MudBlazor = 1,

    /// <summary>
    /// A dark-teal editorial dashboard theme.
    /// </summary>
    HermesTeal = 2,

    /// <summary>
    /// A terminal-green hacker console theme.
    /// </summary>
    VibeUsageMatrix = 3,

    /// <summary>
    /// A Material Design 3 inspired theme.
    /// </summary>
    MaterialDesign3 = 4,

    /// <summary>
    /// A Fluent Design inspired theme.
    /// </summary>
    FluentDesign = 5,

    /// <summary>
    /// A light, fresh mint-color theme.
    /// </summary>
    Fresh = 6,

    /// <summary>
    /// A playful pastel macaron theme.
    /// </summary>
    MacaronSweetheart = 7,

    /// <summary>
    /// An ink-wash landscape theme.
    /// </summary>
    InkLandscape = 8,

    /// <summary>
    /// A minimalist ink painting theme.
    /// </summary>
    ZenInk = 9
}
