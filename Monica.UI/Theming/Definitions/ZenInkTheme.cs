using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// A minimalist Chinese ink wash painting theme with ample white space, 
/// utilizing high-contrast black/white tones with elegant cyan-green and ochre accents.
/// </summary>
public sealed class ZenInkTheme : ThemeDefinitionBase
{
    private static readonly string[] SerifFontFamily = ["Noto Serif SC", "Source Han Serif SC", "STZhongsong", "serif"];

    public override string Name => "zen-ink";

    public override string DisplayName => "禅墨";

    public override string Description => "极简中国水墨画风格，大面积留白与笔触之美";

    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Ascetic;

    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.AtomOneDark;

    public override MudTheme CreateTheme()
    {
        return new MudTheme
        {
            PaletteLight = CreateLightPalette(),
            PaletteDark = CreateDarkPalette(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "2px", // Sharp, minimalist edges resembling seal stamps
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px"
            },
            Typography = CreateTypography(),
            Shadows = new Shadow
            {
                Elevation = CreateShadows()
            },
            ZIndex = new ZIndex
            {
                Drawer = 1200,
                AppBar = 1100,
                Dialog = 1300,
                Popover = 1400,
                Snackbar = 1500,
                Tooltip = 1600
            }
        };
    }

    private static PaletteLight CreateLightPalette()
    {
        return new PaletteLight
        {
            Primary = "#3f5f50", // Deep Cyan-Green
            PrimaryLighten = "#5c8773",
            PrimaryDarken = "#253b31",
            PrimaryContrastText = "#ffffff",

            Secondary = "#8b4513", // Ochre (SaddleBrown variant)
            SecondaryLighten = "#af591a",
            SecondaryDarken = "#5e2f0d",
            SecondaryContrastText = "#ffffff",

            Tertiary = "#595959", // Medium gray ink
            TertiaryLighten = "#737373",
            TertiaryDarken = "#404040",
            TertiaryContrastText = "#ffffff",

            Info = "#47617a", // Indigo ink
            InfoLighten = "#6788a8",
            InfoDarken = "#2c3d4e",
            InfoContrastText = "#ffffff",

            Success = "#4b7060",
            SuccessLighten = "#6a9c86",
            SuccessDarken = "#31493f",
            SuccessContrastText = "#ffffff",

            Warning = "#a66829",
            WarningLighten = "#cc873f",
            WarningDarken = "#784a1d",
            WarningContrastText = "#ffffff",

            Error = "#8c2e2e", // Cinnabar seal red
            ErrorLighten = "#b34040",
            ErrorDarken = "#661f1f",
            ErrorContrastText = "#ffffff",

            Dark = "#121212", // Dense black ink
            DarkLighten = "#2b2b2b",
            DarkDarken = "#000000",
            DarkContrastText = "#ffffff",

            Background = "#ffffff", // Pure white space
            BackgroundGray = "#f5f5f5", // Light wash
            Surface = "#fcfcfc",

            DrawerBackground = "#ffffff",
            DrawerText = "#1a1a1a",
            DrawerIcon = "#595959",

            AppbarBackground = "rgba(255, 255, 255, 0.95)",
            AppbarText = "#1a1a1a",

            TextPrimary = "#1a1a1a", // Charcoal black
            TextSecondary = "#595959", // Diluted black
            TextDisabled = "#9e9e9e",

            ActionDefault = "#595959",
            ActionDisabled = "#bfbfbf",
            ActionDisabledBackground = "#f0f0f0",

            Divider = "rgba(0, 0, 0, 0.12)",
            DividerLight = "rgba(0, 0, 0, 0.06)",
            LinesDefault = "rgba(0, 0, 0, 0.12)",
            LinesInputs = "rgba(0, 0, 0, 0.2)",

            TableLines = "rgba(0, 0, 0, 0.08)",
            TableStriped = "#fafafa",
            TableHover = "#f0f0f0",

            OverlayDark = "rgba(18, 18, 18, 0.5)",
            OverlayLight = "rgba(255, 255, 255, 0.8)",

            HoverOpacity = 0.04,

            GrayDefault = "#8c8c8c",
            GrayLight = "#d9d9d9",
            GrayLighter = "#f0f0f0",
            GrayDark = "#595959",
            GrayDarker = "#262626"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#6a9c86", // Lighter Cyan-Green for dark mode
            PrimaryLighten = "#8bc2aa",
            PrimaryDarken = "#4b7060",
            PrimaryContrastText = "#121212",

            Secondary = "#bc7441", // Lighter Ochre
            SecondaryLighten = "#de9664",
            SecondaryDarken = "#8b4513",
            SecondaryContrastText = "#121212",

            Tertiary = "#a6a6a6", // Light gray
            TertiaryLighten = "#cccccc",
            TertiaryDarken = "#737373",
            TertiaryContrastText = "#121212",

            Info = "#7d9fbf",
            InfoLighten = "#a4c4e5",
            InfoDarken = "#567795",
            InfoContrastText = "#121212",

            Success = "#7ba892",
            SuccessLighten = "#a0cdb6",
            SuccessDarken = "#557f6b",
            SuccessContrastText = "#121212",

            Warning = "#d69956",
            WarningLighten = "#f7c081",
            WarningDarken = "#a16b32",
            WarningContrastText = "#121212",

            Error = "#c95d5d",
            ErrorLighten = "#e88282",
            ErrorDarken = "#9e3d3d",
            ErrorContrastText = "#121212",

            Dark = "#f0f0f0",
            DarkLighten = "#ffffff",
            DarkDarken = "#d9d9d9",
            DarkContrastText = "#121212",

            Background = "#121212", // Dense black background
            BackgroundGray = "#1a1a1a",
            Surface = "#1c1c1c",

            DrawerBackground = "#141414",
            DrawerText = "#e0e0e0",
            DrawerIcon = "#a6a6a6",

            AppbarBackground = "rgba(18, 18, 18, 0.95)",
            AppbarText = "#e0e0e0",

            TextPrimary = "#e0e0e0",
            TextSecondary = "#a6a6a6",
            TextDisabled = "#737373",

            ActionDefault = "#a6a6a6",
            ActionDisabled = "#595959",
            ActionDisabledBackground = "#262626",

            Divider = "rgba(255, 255, 255, 0.12)",
            DividerLight = "rgba(255, 255, 255, 0.06)",
            LinesDefault = "rgba(255, 255, 255, 0.12)",
            LinesInputs = "rgba(255, 255, 255, 0.2)",

            TableLines = "rgba(255, 255, 255, 0.08)",
            TableStriped = "#1a1a1a",
            TableHover = "#262626",

            OverlayDark = "rgba(0, 0, 0, 0.7)",
            OverlayLight = "rgba(255, 255, 255, 0.15)",

            HoverOpacity = 0.08,

            GrayDefault = "#8c8c8c",
            GrayLight = "#595959",
            GrayLighter = "#404040",
            GrayDark = "#bfbfbf",
            GrayDarker = "#e0e0e0"
        };
    }

    private static Typography CreateTypography()
    {
        return new Typography
        {
            Default = CreateTypography<DefaultTypography>("0.875rem", "400", "1.7"),
            H1 = CreateTypography<H1Typography>("3rem", "500", "1.2"),
            H2 = CreateTypography<H2Typography>("2.5rem", "500", "1.3"),
            H3 = CreateTypography<H3Typography>("2rem", "500", "1.4"),
            H4 = CreateTypography<H4Typography>("1.5rem", "500", "1.5"),
            H5 = CreateTypography<H5Typography>("1.25rem", "600", "1.6"),
            H6 = CreateTypography<H6Typography>("1.125rem", "600", "1.6"),
            Subtitle1 = CreateTypography<Subtitle1Typography>("1rem", "500", "1.7"),
            Subtitle2 = CreateTypography<Subtitle2Typography>("0.875rem", "500", "1.7"),
            Body1 = CreateTypography<Body1Typography>("1rem", "400", "1.8"),
            Body2 = CreateTypography<Body2Typography>("0.875rem", "400", "1.7"),
            Button = CreateButtonTypography(),
            Caption = CreateTypography<CaptionTypography>("0.75rem", "400", "1.6"),
            Overline = CreateOverlineTypography()
        };
    }

    private static TTypography CreateTypography<TTypography>(
        string fontSize,
        string fontWeight,
        string lineHeight)
        where TTypography : BaseTypography, new()
    {
        return new TTypography
        {
            FontFamily = SerifFontFamily,
            FontSize = fontSize,
            FontWeight = fontWeight,
            LineHeight = lineHeight,
            LetterSpacing = "0.02em" // Slight spacing for elegance
        };
    }

    private static ButtonTypography CreateButtonTypography()
    {
        return new ButtonTypography
        {
            FontFamily = SerifFontFamily,
            FontSize = "0.875rem",
            FontWeight = "500",
            LineHeight = "1.5",
            LetterSpacing = "0.05em",
            TextTransform = "none"
        };
    }

    private static OverlineTypography CreateOverlineTypography()
    {
        return new OverlineTypography
        {
            FontFamily = SerifFontFamily,
            FontSize = "0.75rem",
            FontWeight = "600",
            LineHeight = "1.6",
            LetterSpacing = "0.1em",
            TextTransform = "uppercase"
        };
    }

    private static string[] CreateShadows()
    {
        // Minimalist, faint shadows resembling ink spread
        return
        [
            "none",
            "0 2px 4px rgba(0, 0, 0, 0.03)",
            "0 3px 8px rgba(0, 0, 0, 0.04)",
            "0 4px 12px rgba(0, 0, 0, 0.05)",
            "0 6px 16px rgba(0, 0, 0, 0.06)",
            "0 8px 20px rgba(0, 0, 0, 0.07)",
            "0 10px 24px rgba(0, 0, 0, 0.08)",
            "0 12px 28px rgba(0, 0, 0, 0.08)",
            "0 14px 32px rgba(0, 0, 0, 0.09)",
            "0 16px 36px rgba(0, 0, 0, 0.09)",
            "0 18px 40px rgba(0, 0, 0, 0.10)",
            "0 20px 44px rgba(0, 0, 0, 0.10)",
            "0 22px 48px rgba(0, 0, 0, 0.11)",
            "0 24px 52px rgba(0, 0, 0, 0.11)",
            "0 26px 56px rgba(0, 0, 0, 0.12)",
            "0 28px 60px rgba(0, 0, 0, 0.12)",
            "0 30px 64px rgba(0, 0, 0, 0.13)",
            "0 32px 68px rgba(0, 0, 0, 0.13)",
            "0 34px 72px rgba(0, 0, 0, 0.14)",
            "0 36px 76px rgba(0, 0, 0, 0.14)",
            "0 38px 80px rgba(0, 0, 0, 0.15)",
            "0 40px 84px rgba(0, 0, 0, 0.15)",
            "0 42px 88px rgba(0, 0, 0, 0.16)",
            "0 44px 92px rgba(0, 0, 0, 0.16)",
            "0 46px 96px rgba(0, 0, 0, 0.17)",
            "0 48px 100px rgba(0, 0, 0, 0.17)"
        ];
    }
}
