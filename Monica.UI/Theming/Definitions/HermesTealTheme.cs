using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Hermes-inspired editorial dashboard theme with square surfaces, warm ivory text,
/// and a restrained dark teal shell.
/// </summary>
public sealed class HermesTealTheme : ThemeDefinitionBase
{
    public override string Name => "hermes-teal";

    public override string DisplayName => "Hermes Teal";

    public override string Description => "Editorial dark teal dashboard inspired by the Hermes Agent web UI.";

    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Default;

    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.MonokaiSublime;

    public override MudTheme CreateTheme()
    {
        return new MudTheme
        {
            PaletteLight = CreateLightPalette(),
            PaletteDark = CreateDarkPalette(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "0px",
                AppbarHeight = "48px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px"
            },
            Typography = CreateTypography(),
            Shadows = new Shadow
            {
                Elevation = CreateFlatShadows()
            }
        };
    }

    private static PaletteLight CreateLightPalette()
    {
        return new PaletteLight
        {
            Primary = "#14302c",
            PrimaryLighten = "#20423d",
            PrimaryDarken = "#0d221f",
            PrimaryContrastText = "#f7ecd8",

            Secondary = "#5e756c",
            SecondaryLighten = "#7c9188",
            SecondaryDarken = "#465a53",
            SecondaryContrastText = "#f7ecd8",

            Tertiary = "#fff6ea",
            TertiaryLighten = "#ffffff",
            TertiaryDarken = "#f1e6d6",
            TertiaryContrastText = "#14302c",

            Info = "#356d68",
            InfoLighten = "#4d8a84",
            InfoDarken = "#25514d",
            InfoContrastText = "#f7ecd8",

            Success = "#2f855a",
            SuccessLighten = "#48bb78",
            SuccessDarken = "#276749",
            SuccessContrastText = "#f7ecd8",

            Warning = "#b7791f",
            WarningLighten = "#d69e2e",
            WarningDarken = "#975a16",
            WarningContrastText = "#fff8e8",

            Error = "#c53030",
            ErrorLighten = "#e53e3e",
            ErrorDarken = "#9b2c2c",
            ErrorContrastText = "#fff8e8",

            Dark = "#102927",
            DarkLighten = "#1b3c38",
            DarkDarken = "#091b19",
            DarkContrastText = "#f7ecd8",

            Background = "#f7ecd8",
            BackgroundGray = "#efe1c9",
            Surface = "#fff6ea",

            DrawerBackground = "#f7ecd8",
            DrawerText = "#102927",
            DrawerIcon = "#14302c",

            AppbarBackground = "rgba(247, 236, 216, 0.92)",
            AppbarText = "#102927",

            TextPrimary = "#102927",
            TextSecondary = "#5e756c",
            TextDisabled = "#9aa7a1",

            ActionDefault = "#5e756c",
            ActionDisabled = "#c7bba7",
            ActionDisabledBackground = "#efe1c9",

            Divider = "rgba(16, 41, 39, 0.14)",
            DividerLight = "rgba(16, 41, 39, 0.08)",
            LinesDefault = "rgba(16, 41, 39, 0.14)",
            LinesInputs = "rgba(16, 41, 39, 0.20)",

            TableLines = "rgba(16, 41, 39, 0.14)",
            TableStriped = "#f3e6d1",
            TableHover = "#ede0cb",

            OverlayDark = "rgba(16, 41, 39, 0.24)",
            OverlayLight = "rgba(255, 246, 234, 0.72)",

            HoverOpacity = 0.04,

            GrayDefault = "#5e756c",
            GrayLight = "#c5bbb0",
            GrayLighter = "#ece1d4",
            GrayDark = "#415450",
            GrayDarker = "#102927"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#ffe6cb",
            PrimaryLighten = "#fff0df",
            PrimaryDarken = "#e8cfb3",
            PrimaryContrastText = "#041c1c",

            Secondary = "#8aaa9a",
            SecondaryLighten = "#a9c0b4",
            SecondaryDarken = "#6f8f81",
            SecondaryContrastText = "#041c1c",

            Tertiary = "#132826",
            TertiaryLighten = "#1d302e",
            TertiaryDarken = "#0b1f1f",
            TertiaryContrastText = "#ffe6cb",

            Info = "#9dd2c7",
            InfoLighten = "#b6e2d8",
            InfoDarken = "#7ab7aa",
            InfoContrastText = "#041c1c",

            Success = "#4ade80",
            SuccessLighten = "#86efac",
            SuccessDarken = "#22c55e",
            SuccessContrastText = "#041c1c",

            Warning = "#ffbd38",
            WarningLighten = "#ffd36f",
            WarningDarken = "#e3a521",
            WarningContrastText = "#041c1c",

            Error = "#f87171",
            ErrorLighten = "#fca5a5",
            ErrorDarken = "#ef4444",
            ErrorContrastText = "#041c1c",

            Dark = "#f7ead7",
            DarkLighten = "#fff6ea",
            DarkDarken = "#d8c2aa",
            DarkContrastText = "#041c1c",

            Background = "#041c1c",
            BackgroundGray = "#0b1f1f",
            Surface = "#0e2423",

            DrawerBackground = "#041c1c",
            DrawerText = "#ffe6cb",
            DrawerIcon = "#8aaa9a",

            AppbarBackground = "rgba(4, 28, 28, 0.92)",
            AppbarText = "#ffe6cb",

            TextPrimary = "#ffe6cb",
            TextSecondary = "#8aaa9a",
            TextDisabled = "#5f756d",

            ActionDefault = "#8aaa9a",
            ActionDisabled = "#334845",
            ActionDisabledBackground = "#132826",

            Divider = "rgba(255, 230, 203, 0.15)",
            DividerLight = "rgba(255, 230, 203, 0.08)",
            LinesDefault = "rgba(255, 230, 203, 0.15)",
            LinesInputs = "rgba(255, 230, 203, 0.22)",

            TableLines = "rgba(255, 230, 203, 0.15)",
            TableStriped = "#0b2121",
            TableHover = "#132826",

            OverlayDark = "rgba(4, 28, 28, 0.82)",
            OverlayLight = "rgba(255, 230, 203, 0.12)",

            HoverOpacity = 0.06,

            GrayDefault = "#8aaa9a",
            GrayLight = "#c6d3cd",
            GrayLighter = "#e8ede7",
            GrayDark = "#5f756d",
            GrayDarker = "#324542"
        };
    }

    private static Typography CreateTypography()
    {
        var editorialFamily = new[] { "Mondwest", "Noto Serif SC", "Georgia", "serif" };
        var compressedFamily = new[] { "RulesCompressed", "Noto Serif SC", "Arial Narrow", "sans-serif" };

        return new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = editorialFamily,
                FontSize = "0.95rem",
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "0.01em"
            },
            H1 = new H1Typography
            {
                FontFamily = editorialFamily,
                FontSize = "3rem",
                FontWeight = "400",
                LineHeight = "1.05",
                LetterSpacing = "-0.02em"
            },
            H2 = new H2Typography
            {
                FontFamily = editorialFamily,
                FontSize = "2.5rem",
                FontWeight = "400",
                LineHeight = "1.08",
                LetterSpacing = "-0.02em"
            },
            H3 = new H3Typography
            {
                FontFamily = editorialFamily,
                FontSize = "2rem",
                FontWeight = "400",
                LineHeight = "1.12",
                LetterSpacing = "-0.01em"
            },
            H4 = new H4Typography
            {
                FontFamily = editorialFamily,
                FontSize = "1.5rem",
                FontWeight = "500",
                LineHeight = "1.2",
                LetterSpacing = "-0.01em"
            },
            H5 = new H5Typography
            {
                FontFamily = editorialFamily,
                FontSize = "1.125rem",
                FontWeight = "600",
                LineHeight = "1.3",
                LetterSpacing = "0"
            },
            H6 = new H6Typography
            {
                FontFamily = editorialFamily,
                FontSize = "1rem",
                FontWeight = "600",
                LineHeight = "1.35",
                LetterSpacing = "0.01em"
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = editorialFamily,
                FontSize = "1rem",
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "0.02em"
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = editorialFamily,
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.45",
                LetterSpacing = "0.02em"
            },
            Body1 = new Body1Typography
            {
                FontFamily = editorialFamily,
                FontSize = "1rem",
                FontWeight = "400",
                LineHeight = "1.65",
                LetterSpacing = "0.01em"
            },
            Body2 = new Body2Typography
            {
                FontFamily = editorialFamily,
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.55",
                LetterSpacing = "0.01em"
            },
            Button = new ButtonTypography
            {
                FontFamily = editorialFamily,
                FontSize = "0.8125rem",
                FontWeight = "400",
                LineHeight = "1.35",
                LetterSpacing = "0.06em",
                TextTransform = "none"
            },
            Caption = new CaptionTypography
            {
                FontFamily = compressedFamily,
                FontSize = "0.72rem",
                FontWeight = "400",
                LineHeight = "1.2",
                LetterSpacing = "0.12em",
                TextTransform = "uppercase"
            },
            Overline = new OverlineTypography
            {
                FontFamily = compressedFamily,
                FontSize = "0.68rem",
                FontWeight = "500",
                LineHeight = "1.1",
                LetterSpacing = "0.16em",
                TextTransform = "uppercase"
            }
        };
    }

    private static string[] CreateFlatShadows()
    {
        // MudBlazor generates CSS variables for elevations 0 through 25 inclusive.
        var elevations = new string[26];
        Array.Fill(elevations, "none");
        return elevations;
    }
}
