using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Material Design 3 theme using Material Web v0.192 color, shape, and typography tokens.
/// </summary>
public sealed class MaterialDesign3Theme : ThemeDefinitionBase
{
    public override string Name => "material-design-3";

    public override string DisplayName => "Material Design 3";

    public override string Description => "Material Design 3 token-based theme with soft surfaces, rounded shapes, and expressive typography.";

    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;

    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.AtomOneDark;

    public override MudTheme CreateTheme()
    {
        return new MudTheme
        {
            PaletteLight = CreateLightPalette(),
            PaletteDark = CreateDarkPalette(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px",
                DrawerMiniWidthLeft = "80px",
                DrawerMiniWidthRight = "80px"
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
            Primary = "#6750a4",
            PrimaryLighten = "#7f67be",
            PrimaryDarken = "#4f378b",
            PrimaryContrastText = "#ffffff",

            Secondary = "#625b71",
            SecondaryLighten = "#7a7289",
            SecondaryDarken = "#4a4458",
            SecondaryContrastText = "#ffffff",

            Tertiary = "#7d5260",
            TertiaryLighten = "#986977",
            TertiaryDarken = "#633b48",
            TertiaryContrastText = "#ffffff",

            Info = "#006a6a",
            InfoLighten = "#268080",
            InfoDarken = "#005050",
            InfoContrastText = "#ffffff",

            Success = "#386a20",
            SuccessLighten = "#4f7f34",
            SuccessDarken = "#245109",
            SuccessContrastText = "#ffffff",

            Warning = "#7d5700",
            WarningLighten = "#986b00",
            WarningDarken = "#604200",
            WarningContrastText = "#ffffff",

            Error = "#b3261e",
            ErrorLighten = "#dc362e",
            ErrorDarken = "#8c1d18",
            ErrorContrastText = "#ffffff",

            Dark = "#1d1b20",
            DarkLighten = "#322f35",
            DarkDarken = "#000000",
            DarkContrastText = "#fef7ff",

            Background = "#fef7ff",
            BackgroundGray = "#f3edf7",
            Surface = "#fffbff",

            DrawerBackground = "#fef7ff",
            DrawerText = "#1d1b20",
            DrawerIcon = "#49454f",

            AppbarBackground = "#fef7ff",
            AppbarText = "#1d1b20",

            TextPrimary = "#1d1b20",
            TextSecondary = "#49454f",
            TextDisabled = "#79747e",

            ActionDefault = "#49454f",
            ActionDisabled = "rgba(29, 27, 32, 0.38)",
            ActionDisabledBackground = "rgba(29, 27, 32, 0.12)",

            Divider = "#cac4d0",
            DividerLight = "#e7e0ec",
            LinesDefault = "#cac4d0",
            LinesInputs = "#79747e",

            TableLines = "#cac4d0",
            TableStriped = "#f7f2fa",
            TableHover = "#f3edf7",

            OverlayDark = "rgba(29, 27, 32, 0.40)",
            OverlayLight = "rgba(255, 251, 255, 0.72)",

            HoverOpacity = 0.08,

            GrayDefault = "#79747e",
            GrayLight = "#cac4d0",
            GrayLighter = "#f3edf7",
            GrayDark = "#49454f",
            GrayDarker = "#1d1b20"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#d0bcff",
            PrimaryLighten = "#eaddff",
            PrimaryDarken = "#b69df8",
            PrimaryContrastText = "#381e72",

            Secondary = "#ccc2dc",
            SecondaryLighten = "#e8def8",
            SecondaryDarken = "#b0a7c0",
            SecondaryContrastText = "#332d41",

            Tertiary = "#efb8c8",
            TertiaryLighten = "#ffd8e4",
            TertiaryDarken = "#d29dac",
            TertiaryContrastText = "#492532",

            Info = "#80d8d8",
            InfoLighten = "#9ff0f0",
            InfoDarken = "#5fbcbc",
            InfoContrastText = "#003737",

            Success = "#9ad67d",
            SuccessLighten = "#b5f397",
            SuccessDarken = "#7fba63",
            SuccessContrastText = "#113800",

            Warning = "#e4c46b",
            WarningLighten = "#ffdf86",
            WarningDarken = "#c7a850",
            WarningContrastText = "#3f2e00",

            Error = "#f2b8b5",
            ErrorLighten = "#f9dedc",
            ErrorDarken = "#ec928e",
            ErrorContrastText = "#601410",

            Dark = "#e6e0e9",
            DarkLighten = "#ffffff",
            DarkDarken = "#cac4d0",
            DarkContrastText = "#141218",

            Background = "#141218",
            BackgroundGray = "#211f26",
            Surface = "#141218",

            DrawerBackground = "#141218",
            DrawerText = "#e6e0e9",
            DrawerIcon = "#cac4d0",

            AppbarBackground = "#141218",
            AppbarText = "#e6e0e9",

            TextPrimary = "#e6e0e9",
            TextSecondary = "#cac4d0",
            TextDisabled = "#938f99",

            ActionDefault = "#cac4d0",
            ActionDisabled = "rgba(230, 224, 233, 0.38)",
            ActionDisabledBackground = "rgba(230, 224, 233, 0.12)",

            Divider = "#49454f",
            DividerLight = "#322f37",
            LinesDefault = "#49454f",
            LinesInputs = "#938f99",

            TableLines = "#49454f",
            TableStriped = "#1d1b20",
            TableHover = "#211f26",

            OverlayDark = "rgba(0, 0, 0, 0.64)",
            OverlayLight = "rgba(230, 224, 233, 0.12)",

            HoverOpacity = 0.08,

            GrayDefault = "#938f99",
            GrayLight = "#cac4d0",
            GrayLighter = "#e6e0e9",
            GrayDark = "#605d66",
            GrayDarker = "#49454f"
        };
    }

    private static Typography CreateTypography()
    {
        var displayFamily = new[] { "MoM3Display", "MoM3Text", "Roboto", "sans-serif" };
        var textFamily = new[] { "MoM3Text", "Roboto", "sans-serif" };

        return new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = textFamily,
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.25rem",
                LetterSpacing = "0.015625rem"
            },
            H1 = new H1Typography { FontFamily = displayFamily, FontSize = "3.5625rem", FontWeight = "400", LineHeight = "4rem", LetterSpacing = "-0.015625rem" },
            H2 = new H2Typography { FontFamily = displayFamily, FontSize = "2.8125rem", FontWeight = "400", LineHeight = "3.25rem", LetterSpacing = "0" },
            H3 = new H3Typography { FontFamily = displayFamily, FontSize = "2.25rem", FontWeight = "400", LineHeight = "2.75rem", LetterSpacing = "0" },
            H4 = new H4Typography { FontFamily = displayFamily, FontSize = "2rem", FontWeight = "400", LineHeight = "2.5rem", LetterSpacing = "0" },
            H5 = new H5Typography { FontFamily = displayFamily, FontSize = "1.75rem", FontWeight = "400", LineHeight = "2.25rem", LetterSpacing = "0" },
            H6 = new H6Typography { FontFamily = displayFamily, FontSize = "1.5rem", FontWeight = "400", LineHeight = "2rem", LetterSpacing = "0" },
            Subtitle1 = new Subtitle1Typography { FontFamily = displayFamily, FontSize = "1.375rem", FontWeight = "400", LineHeight = "1.75rem", LetterSpacing = "0" },
            Subtitle2 = new Subtitle2Typography { FontFamily = textFamily, FontSize = "1rem", FontWeight = "500", LineHeight = "1.5rem", LetterSpacing = "0.009375rem" },
            Body1 = new Body1Typography { FontFamily = textFamily, FontSize = "1rem", FontWeight = "400", LineHeight = "1.5rem", LetterSpacing = "0.03125rem" },
            Body2 = new Body2Typography { FontFamily = textFamily, FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.25rem", LetterSpacing = "0.015625rem" },
            Button = new ButtonTypography { FontFamily = textFamily, FontSize = "0.875rem", FontWeight = "500", LineHeight = "1.25rem", LetterSpacing = "0.00625rem", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = textFamily, FontSize = "0.6875rem", FontWeight = "500", LineHeight = "1rem", LetterSpacing = "0.03125rem" },
            Overline = new OverlineTypography { FontFamily = textFamily, FontSize = "0.75rem", FontWeight = "500", LineHeight = "1rem", LetterSpacing = "0.03125rem", TextTransform = "uppercase" }
        };
    }

    private static string[] CreateElevation()
    {
        const string level0 = "none";
        const string level1 = "0 1px 2px rgba(0, 0, 0, 0.30), 0 1px 3px 1px rgba(0, 0, 0, 0.15)";
        const string level2 = "0 1px 2px rgba(0, 0, 0, 0.30), 0 2px 6px 2px rgba(0, 0, 0, 0.15)";
        const string level3 = "0 4px 8px 3px rgba(0, 0, 0, 0.15), 0 1px 3px rgba(0, 0, 0, 0.30)";
        const string level4 = "0 6px 10px 4px rgba(0, 0, 0, 0.15), 0 2px 3px rgba(0, 0, 0, 0.30)";
        const string level5 = "0 8px 12px 6px rgba(0, 0, 0, 0.15), 0 4px 4px rgba(0, 0, 0, 0.30)";

        return
        [
            level0,
            level1,
            level1,
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
            level5,
            level5,
            level5,
            level5,
            level5,
            level5,
            level5,
            level5
        ];
    }
}
