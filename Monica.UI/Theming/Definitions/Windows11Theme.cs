using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Windows 11 inspired theme that maps Fluent neutral layers, sparse accent usage, Segoe typography,
/// rounded geometry, and subtle elevation into MudBlazor tokens.
/// </summary>
public sealed class Windows11Theme : ThemeDefinitionBase
{
    /// <inheritdoc />
    public override string Name => "windows-11";

    /// <inheritdoc />
    public override string DisplayName => "Windows 11";

    /// <inheritdoc />
    public override string Description => "Fluent Windows 11 styling with Mica-inspired layers, acrylic flyouts, Segoe typography, and soft rounded controls.";

    /// <inheritdoc />
    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;

    /// <inheritdoc />
    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.GithubDark;

    /// <inheritdoc />
    public override MudTheme CreateTheme()
    {
        return new MudTheme
        {
            PaletteLight = CreateLightPalette(),
            PaletteDark = CreateDarkPalette(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "4px",
                AppbarHeight = "48px",
                DrawerWidthLeft = "272px",
                DrawerWidthRight = "272px",
                DrawerMiniWidthLeft = "56px",
                DrawerMiniWidthRight = "56px"
            },
            Typography = CreateTypography(),
            Shadows = new Shadow
            {
                Elevation = CreateElevation()
            }
        };
    }

    private static PaletteLight CreateLightPalette()
    {
        return new PaletteLight
        {
            Primary = "#0067c0",
            PrimaryLighten = "#2f8edb",
            PrimaryDarken = "#005a9e",
            PrimaryContrastText = "#ffffff",

            Secondary = "#5d5a64",
            SecondaryLighten = "#817d87",
            SecondaryDarken = "#45424a",
            SecondaryContrastText = "#ffffff",

            Tertiary = "#767676",
            TertiaryLighten = "#8f8f8f",
            TertiaryDarken = "#5f5f5f",
            TertiaryContrastText = "#ffffff",

            Info = "#0078d4",
            InfoLighten = "#2b9fed",
            InfoDarken = "#005a9e",
            InfoContrastText = "#ffffff",

            Success = "#107c10",
            SuccessLighten = "#2e9a2e",
            SuccessDarken = "#0b5f0b",
            SuccessContrastText = "#ffffff",

            Warning = "#9d5d00",
            WarningLighten = "#c27a13",
            WarningDarken = "#744400",
            WarningContrastText = "#ffffff",

            Error = "#c42b1c",
            ErrorLighten = "#e05142",
            ErrorDarken = "#9f1f14",
            ErrorContrastText = "#ffffff",

            Dark = "#1a1a1a",
            DarkLighten = "#2b2b2b",
            DarkDarken = "#000000",
            DarkContrastText = "#ffffff",

            Background = "#f3f3f3",
            BackgroundGray = "#eeeeee",
            Surface = "#fbfbfb",

            DrawerBackground = "#f7f7f7",
            DrawerText = "#1a1a1a",
            DrawerIcon = "#5c5c5c",

            AppbarBackground = "#f3f3f3",
            AppbarText = "#1a1a1a",

            TextPrimary = "#1a1a1a",
            TextSecondary = "#5c5c5c",
            TextDisabled = "#9a9a9a",

            ActionDefault = "#5c5c5c",
            ActionDisabled = "#9a9a9a",
            ActionDisabledBackground = "#e5e5e5",

            Divider = "#e5e5e5",
            DividerLight = "#f0f0f0",
            LinesDefault = "#e5e5e5",
            LinesInputs = "#8a8a8a",

            TableLines = "#e5e5e5",
            TableStriped = "#f7f7f7",
            TableHover = "#f0f6fc",

            OverlayDark = "rgba(0, 0, 0, 0.32)",
            OverlayLight = "rgba(255, 255, 255, 0.70)",

            HoverOpacity = 0.04,

            GrayDefault = "#767676",
            GrayLight = "#d6d6d6",
            GrayLighter = "#f3f3f3",
            GrayDark = "#5c5c5c",
            GrayDarker = "#2b2b2b"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#60cdff",
            PrimaryLighten = "#8cddff",
            PrimaryDarken = "#3aa0d8",
            PrimaryContrastText = "#00344a",

            Secondary = "#c9c5d0",
            SecondaryLighten = "#e1dde8",
            SecondaryDarken = "#aaa5b1",
            SecondaryContrastText = "#1f1f1f",

            Tertiary = "#a8a8a8",
            TertiaryLighten = "#c8c8c8",
            TertiaryDarken = "#858585",
            TertiaryContrastText = "#1f1f1f",

            Info = "#60cdff",
            InfoLighten = "#8cddff",
            InfoDarken = "#3aa0d8",
            InfoContrastText = "#00344a",

            Success = "#6ccb5f",
            SuccessLighten = "#8de082",
            SuccessDarken = "#4daa42",
            SuccessContrastText = "#062d05",

            Warning = "#fce100",
            WarningLighten = "#fff05c",
            WarningDarken = "#d7bf00",
            WarningContrastText = "#3a3000",

            Error = "#ff99a4",
            ErrorLighten = "#ffc0c7",
            ErrorDarken = "#e87986",
            ErrorContrastText = "#4f050d",

            Dark = "#f3f3f3",
            DarkLighten = "#ffffff",
            DarkDarken = "#d6d6d6",
            DarkContrastText = "#1f1f1f",

            Background = "#202020",
            BackgroundGray = "#1c1c1c",
            Surface = "#2b2b2b",

            DrawerBackground = "#252525",
            DrawerText = "#f3f3f3",
            DrawerIcon = "#cfcfcf",

            AppbarBackground = "#202020",
            AppbarText = "#f3f3f3",

            TextPrimary = "#f3f3f3",
            TextSecondary = "#cfcfcf",
            TextDisabled = "#777777",

            ActionDefault = "#cfcfcf",
            ActionDisabled = "#777777",
            ActionDisabledBackground = "#3a3a3a",

            Divider = "#3a3a3a",
            DividerLight = "#303030",
            LinesDefault = "#3a3a3a",
            LinesInputs = "#8a8a8a",

            TableLines = "#3a3a3a",
            TableStriped = "#252525",
            TableHover = "#2f3740",

            OverlayDark = "rgba(0, 0, 0, 0.48)",
            OverlayLight = "rgba(255, 255, 255, 0.10)",

            HoverOpacity = 0.06,

            GrayDefault = "#8a8a8a",
            GrayLight = "#b8b8b8",
            GrayLighter = "#f3f3f3",
            GrayDark = "#5a5a5a",
            GrayDarker = "#3a3a3a"
        };
    }

    private static Typography CreateTypography()
    {
        var textFamily = new[] { "Segoe UI Variable", "Segoe UI", "Roboto", "Arial", "sans-serif" };
        var monoFamily = new[] { "Cascadia Mono", "Consolas", "Geist Mono", "monospace" };

        return new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = textFamily,
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.25rem",
                LetterSpacing = "0"
            },
            H1 = new H1Typography { FontFamily = textFamily, FontSize = "2.5rem", FontWeight = "600", LineHeight = "3rem", LetterSpacing = "0" },
            H2 = new H2Typography { FontFamily = textFamily, FontSize = "2rem", FontWeight = "600", LineHeight = "2.5rem", LetterSpacing = "0" },
            H3 = new H3Typography { FontFamily = textFamily, FontSize = "1.75rem", FontWeight = "600", LineHeight = "2.25rem", LetterSpacing = "0" },
            H4 = new H4Typography { FontFamily = textFamily, FontSize = "1.5rem", FontWeight = "600", LineHeight = "2rem", LetterSpacing = "0" },
            H5 = new H5Typography { FontFamily = textFamily, FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.75rem", LetterSpacing = "0" },
            H6 = new H6Typography { FontFamily = textFamily, FontSize = "1rem", FontWeight = "600", LineHeight = "1.5rem", LetterSpacing = "0" },
            Subtitle1 = new Subtitle1Typography { FontFamily = textFamily, FontSize = "1rem", FontWeight = "600", LineHeight = "1.5rem", LetterSpacing = "0" },
            Subtitle2 = new Subtitle2Typography { FontFamily = textFamily, FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.25rem", LetterSpacing = "0" },
            Body1 = new Body1Typography { FontFamily = textFamily, FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.25rem", LetterSpacing = "0" },
            Body2 = new Body2Typography { FontFamily = textFamily, FontSize = "0.8125rem", FontWeight = "400", LineHeight = "1.125rem", LetterSpacing = "0" },
            Button = new ButtonTypography { FontFamily = textFamily, FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.25rem", LetterSpacing = "0", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = textFamily, FontSize = "0.75rem", FontWeight = "400", LineHeight = "1rem", LetterSpacing = "0" },
            Overline = new OverlineTypography { FontFamily = monoFamily, FontSize = "0.75rem", FontWeight = "600", LineHeight = "1rem", LetterSpacing = "0", TextTransform = "uppercase" }
        };
    }

    private static string[] CreateElevation()
    {
        const string level0 = "none";
        const string level1 = "0 1px 2px rgba(0, 0, 0, 0.12)";
        const string level2 = "0 2px 4px rgba(0, 0, 0, 0.14)";
        const string level3 = "0 4px 8px rgba(0, 0, 0, 0.18)";
        const string level4 = "0 8px 16px rgba(0, 0, 0, 0.22)";
        const string level5 = "0 16px 32px rgba(0, 0, 0, 0.26)";
        const string level6 = "0 32px 64px rgba(0, 0, 0, 0.30)";

        return
        [
            level0,
            level1,
            level1,
            level2,
            level2,
            level2,
            level3,
            level3,
            level3,
            level4,
            level4,
            level4,
            level4,
            level5,
            level5,
            level5,
            level5,
            level5,
            level5,
            level6,
            level6,
            level6,
            level6,
            level6,
            level6,
            level6
        ];
    }
}
