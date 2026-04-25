using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Restrained ink-wash theme with warm paper surfaces, charcoal night surfaces, and serif typography.
/// </summary>
public sealed class InkLandscapeTheme : ThemeDefinitionBase
{
    private static readonly string[] SerifFontFamily = ["Noto Serif SC", "Source Han Serif SC", "serif"];

    public override string Name => "ink-landscape";

    public override string DisplayName => "墨韵山水";

    public override string Description => "水墨留白风格，沉静克制";

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
                DefaultBorderRadius = "6px",
                AppbarHeight = "56px",
                DrawerWidthLeft = "272px",
                DrawerWidthRight = "272px",
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
            Primary = "#2f3a34",
            PrimaryLighten = "#526158",
            PrimaryDarken = "#1d2722",
            PrimaryContrastText = "#fffdf8",

            Secondary = "#756853",
            SecondaryLighten = "#9a8a70",
            SecondaryDarken = "#514737",
            SecondaryContrastText = "#fffdf8",

            Tertiary = "#eadfcd",
            TertiaryLighten = "#f6efe4",
            TertiaryDarken = "#d5c5ab",
            TertiaryContrastText = "#2f332e",

            Info = "#4f7771",
            InfoLighten = "#719790",
            InfoDarken = "#385d58",
            InfoContrastText = "#fffdf8",

            Success = "#53745a",
            SuccessLighten = "#749478",
            SuccessDarken = "#3c5b43",
            SuccessContrastText = "#fffdf8",

            Warning = "#a06f2b",
            WarningLighten = "#bd8a44",
            WarningDarken = "#7d531f",
            WarningContrastText = "#fffdf8",

            Error = "#a33e35",
            ErrorLighten = "#bf5b52",
            ErrorDarken = "#7e2e27",
            ErrorContrastText = "#fffdf8",

            Dark = "#20241f",
            DarkLighten = "#373d35",
            DarkDarken = "#111511",
            DarkContrastText = "#fffdf8",

            Background = "#f7f2e8",
            BackgroundGray = "#eee6d8",
            Surface = "#fffaf1",

            DrawerBackground = "#f5efe3",
            DrawerText = "#2f332e",
            DrawerIcon = "#756853",

            AppbarBackground = "rgba(247, 242, 232, 0.94)",
            AppbarText = "#2f332e",

            TextPrimary = "#252922",
            TextSecondary = "#676150",
            TextDisabled = "#9e9685",

            ActionDefault = "#676150",
            ActionDisabled = "#b8af9e",
            ActionDisabledBackground = "#eee6d8",

            Divider = "rgba(47, 51, 46, 0.16)",
            DividerLight = "rgba(47, 51, 46, 0.08)",
            LinesDefault = "rgba(47, 51, 46, 0.16)",
            LinesInputs = "rgba(47, 51, 46, 0.24)",

            TableLines = "rgba(47, 51, 46, 0.14)",
            TableStriped = "#f2eadc",
            TableHover = "#ebe0cf",

            OverlayDark = "rgba(32, 36, 31, 0.42)",
            OverlayLight = "rgba(255, 250, 241, 0.72)",

            HoverOpacity = 0.05,

            GrayDefault = "#8c8474",
            GrayLight = "#beb5a4",
            GrayLighter = "#e7dece",
            GrayDark = "#676150",
            GrayDarker = "#3f4239"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#c5d0bd",
            PrimaryLighten = "#dce4d6",
            PrimaryDarken = "#aab8a1",
            PrimaryContrastText = "#171b17",

            Secondary = "#bda982",
            SecondaryLighten = "#d4c29d",
            SecondaryDarken = "#9e875f",
            SecondaryContrastText = "#171b17",

            Tertiary = "#2a3029",
            TertiaryLighten = "#363d35",
            TertiaryDarken = "#1f251f",
            TertiaryContrastText = "#efe7d7",

            Info = "#8fb8b0",
            InfoLighten = "#afd0c9",
            InfoDarken = "#6f9b92",
            InfoContrastText = "#111716",

            Success = "#91b893",
            SuccessLighten = "#afd0b0",
            SuccessDarken = "#739c77",
            SuccessContrastText = "#111711",

            Warning = "#d1a85a",
            WarningLighten = "#e0bf7d",
            WarningDarken = "#b68b39",
            WarningContrastText = "#1b1509",

            Error = "#d27a72",
            ErrorLighten = "#e49a94",
            ErrorDarken = "#b75d55",
            ErrorContrastText = "#1c0d0b",

            Dark = "#efe7d7",
            DarkLighten = "#fff8eb",
            DarkDarken = "#d7ccb9",
            DarkContrastText = "#171b17",

            Background = "#171b17",
            BackgroundGray = "#1f241f",
            Surface = "#242a23",

            DrawerBackground = "#1b201b",
            DrawerText = "#ede5d5",
            DrawerIcon = "#b0aa9a",

            AppbarBackground = "rgba(23, 27, 23, 0.94)",
            AppbarText = "#ede5d5",

            TextPrimary = "#f0e8d8",
            TextSecondary = "#b8b09e",
            TextDisabled = "#746f62",

            ActionDefault = "#b8b09e",
            ActionDisabled = "#555146",
            ActionDisabledBackground = "#2a3029",

            Divider = "rgba(240, 232, 216, 0.14)",
            DividerLight = "rgba(240, 232, 216, 0.08)",
            LinesDefault = "rgba(240, 232, 216, 0.14)",
            LinesInputs = "rgba(240, 232, 216, 0.24)",

            TableLines = "rgba(240, 232, 216, 0.12)",
            TableStriped = "#1d221d",
            TableHover = "#2f372f",

            OverlayDark = "rgba(5, 7, 5, 0.76)",
            OverlayLight = "rgba(240, 232, 216, 0.10)",

            HoverOpacity = 0.07,

            GrayDefault = "#8d8879",
            GrayLight = "#b8b09e",
            GrayLighter = "#d8cfbd",
            GrayDark = "#676256",
            GrayDarker = "#3f433b"
        };
    }

    private static Typography CreateTypography()
    {
        return new Typography
        {
            Default = CreateTypography<DefaultTypography>("0.875rem", "400", "1.6"),
            H1 = CreateTypography<H1Typography>("2.75rem", "500", "1.25"),
            H2 = CreateTypography<H2Typography>("2.25rem", "500", "1.3"),
            H3 = CreateTypography<H3Typography>("1.875rem", "500", "1.35"),
            H4 = CreateTypography<H4Typography>("1.5rem", "500", "1.4"),
            H5 = CreateTypography<H5Typography>("1.25rem", "500", "1.45"),
            H6 = CreateTypography<H6Typography>("1.125rem", "600", "1.5"),
            Subtitle1 = CreateTypography<Subtitle1Typography>("1rem", "500", "1.65"),
            Subtitle2 = CreateTypography<Subtitle2Typography>("0.875rem", "500", "1.6"),
            Body1 = CreateTypography<Body1Typography>("1rem", "400", "1.7"),
            Body2 = CreateTypography<Body2Typography>("0.875rem", "400", "1.65"),
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
            LetterSpacing = "0"
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
            LetterSpacing = "0",
            TextTransform = "none"
        };
    }

    private static OverlineTypography CreateOverlineTypography()
    {
        return new OverlineTypography
        {
            FontFamily = SerifFontFamily,
            FontSize = "0.75rem",
            FontWeight = "500",
            LineHeight = "1.6",
            LetterSpacing = "0",
            TextTransform = "none"
        };
    }

    private static string[] CreateShadows()
    {
        return
        [
            "none",
            "0 1px 2px rgba(32, 36, 31, 0.05)",
            "0 2px 6px rgba(32, 36, 31, 0.07)",
            "0 4px 12px rgba(32, 36, 31, 0.08)",
            "0 8px 20px rgba(32, 36, 31, 0.10)",
            "0 12px 28px rgba(32, 36, 31, 0.12)",
            "0 16px 36px rgba(32, 36, 31, 0.14)",
            "0 18px 42px rgba(32, 36, 31, 0.14)",
            "0 20px 48px rgba(32, 36, 31, 0.15)",
            "0 22px 52px rgba(32, 36, 31, 0.15)",
            "0 24px 56px rgba(32, 36, 31, 0.16)",
            "0 26px 60px rgba(32, 36, 31, 0.16)",
            "0 28px 64px rgba(32, 36, 31, 0.17)",
            "0 30px 68px rgba(32, 36, 31, 0.17)",
            "0 32px 72px rgba(32, 36, 31, 0.18)",
            "0 34px 76px rgba(32, 36, 31, 0.18)",
            "0 36px 80px rgba(32, 36, 31, 0.19)",
            "0 38px 84px rgba(32, 36, 31, 0.19)",
            "0 40px 88px rgba(32, 36, 31, 0.20)",
            "0 42px 92px rgba(32, 36, 31, 0.20)",
            "0 44px 96px rgba(32, 36, 31, 0.21)",
            "0 46px 100px rgba(32, 36, 31, 0.21)",
            "0 48px 104px rgba(32, 36, 31, 0.22)",
            "0 50px 108px rgba(32, 36, 31, 0.22)",
            "0 52px 112px rgba(32, 36, 31, 0.23)",
            "0 54px 116px rgba(32, 36, 31, 0.23)"
        ];
    }
}
