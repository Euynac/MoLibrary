using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Hacker-style matrix console theme with phosphor accents,
/// square shells, and terminal-style mono typography.
/// </summary>
public sealed class VibeUsageMatrixTheme : ThemeDefinitionBase
{
    public override MonicaThemeKind Kind => MonicaThemeKind.VibeUsageMatrix;

    public override string DisplayName => "Hacker Matrix";

    public override string Description => "Terminal-green hacker console inspired by the VibeUsage dashboard.";

    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;

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
                AppbarHeight = "52px",
                DrawerWidthLeft = "272px",
                DrawerWidthRight = "272px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px"
            },
            Typography = CreateTypography(),
            Shadows = new Shadow
            {
                Elevation = CreateTerminalShadows()
            }
        };
    }

    private static PaletteLight CreateLightPalette()
    {
        return new PaletteLight
        {
            Primary = "#00bf40",
            PrimaryLighten = "#31d966",
            PrimaryDarken = "#008f30",
            PrimaryContrastText = "#041206",

            Secondary = "#0d6331",
            SecondaryLighten = "#1b8644",
            SecondaryDarken = "#094b24",
            SecondaryContrastText = "#f4fff6",

            Tertiary = "#e5ffe8",
            TertiaryLighten = "#f4fff6",
            TertiaryDarken = "#d0f2d4",
            TertiaryContrastText = "#062b12",

            Info = "#0ea5e9",
            InfoLighten = "#38bdf8",
            InfoDarken = "#0284c7",
            InfoContrastText = "#ffffff",

            Success = "#00bf40",
            SuccessLighten = "#31d966",
            SuccessDarken = "#008f30",
            SuccessContrastText = "#041206",

            Warning = "#c69200",
            WarningLighten = "#e6b400",
            WarningDarken = "#946d00",
            WarningContrastText = "#1d1400",

            Error = "#d44848",
            ErrorLighten = "#eb6666",
            ErrorDarken = "#a93030",
            ErrorContrastText = "#ffffff",

            Dark = "#041206",
            DarkLighten = "#0d2614",
            DarkDarken = "#020803",
            DarkContrastText = "#f4fff6",

            Background = "#effff1",
            BackgroundGray = "#e4f7e7",
            Surface = "#f9fff9",

            DrawerBackground = "rgba(249, 255, 249, 0.94)",
            DrawerText = "#062b12",
            DrawerIcon = "#00a73a",

            AppbarBackground = "rgba(245, 255, 246, 0.86)",
            AppbarText = "#062b12",

            TextPrimary = "#062b12",
            TextSecondary = "#2d6b3b",
            TextDisabled = "#7aa286",

            ActionDefault = "#1c7d3a",
            ActionDisabled = "#bed7c4",
            ActionDisabledBackground = "#e6f4e8",

            Divider = "rgba(0, 179, 54, 0.18)",
            DividerLight = "rgba(0, 179, 54, 0.08)",
            LinesDefault = "rgba(0, 179, 54, 0.18)",
            LinesInputs = "rgba(0, 179, 54, 0.26)",

            TableLines = "rgba(0, 179, 54, 0.18)",
            TableStriped = "#f2fff4",
            TableHover = "#e7fbe9",

            OverlayDark = "rgba(4, 18, 6, 0.18)",
            OverlayLight = "rgba(255, 255, 255, 0.72)",

            HoverOpacity = 0.04,

            GrayDefault = "#2d6b3b",
            GrayLight = "#9bc8a5",
            GrayLighter = "#d7efdc",
            GrayDark = "#145527",
            GrayDarker = "#062b12"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#00ff41",
            PrimaryLighten = "#4dff7b",
            PrimaryDarken = "#00d638",
            PrimaryContrastText = "#020802",

            Secondary = "#95ffb0",
            SecondaryLighten = "#c5ffd1",
            SecondaryDarken = "#5ce685",
            SecondaryContrastText = "#041206",

            Tertiary = "#081508",
            TertiaryLighten = "#102210",
            TertiaryDarken = "#050d05",
            TertiaryContrastText = "#d7ffd9",

            Info = "#4dd5ff",
            InfoLighten = "#8be5ff",
            InfoDarken = "#18bee8",
            InfoContrastText = "#041206",

            Success = "#00ff41",
            SuccessLighten = "#4dff7b",
            SuccessDarken = "#00d638",
            SuccessContrastText = "#020802",

            Warning = "#ffd700",
            WarningLighten = "#ffe45c",
            WarningDarken = "#d0ae00",
            WarningContrastText = "#1d1400",

            Error = "#ff6b6b",
            ErrorLighten = "#ff9b9b",
            ErrorDarken = "#ef4444",
            ErrorContrastText = "#120404",

            Dark = "#e8ffe9",
            DarkLighten = "#ffffff",
            DarkDarken = "#b8ffbf",
            DarkContrastText = "#041206",

            Background = "#050505",
            BackgroundGray = "#040b04",
            Surface = "#061006",

            DrawerBackground = "rgba(0, 10, 0, 0.88)",
            DrawerText = "#d7ffd9",
            DrawerIcon = "#00ff41",

            AppbarBackground = "rgba(0, 10, 0, 0.78)",
            AppbarText = "#d7ffd9",

            TextPrimary = "#d7ffd9",
            TextSecondary = "rgba(0, 255, 65, 0.72)",
            TextDisabled = "rgba(0, 255, 65, 0.35)",

            ActionDefault = "rgba(0, 255, 65, 0.72)",
            ActionDisabled = "rgba(0, 255, 65, 0.24)",
            ActionDisabledBackground = "rgba(0, 255, 65, 0.08)",

            Divider = "rgba(0, 255, 65, 0.18)",
            DividerLight = "rgba(0, 255, 65, 0.08)",
            LinesDefault = "rgba(0, 255, 65, 0.18)",
            LinesInputs = "rgba(0, 255, 65, 0.30)",

            TableLines = "rgba(0, 255, 65, 0.18)",
            TableStriped = "rgba(0, 255, 65, 0.03)",
            TableHover = "rgba(0, 255, 65, 0.07)",

            OverlayDark = "rgba(0, 0, 0, 0.82)",
            OverlayLight = "rgba(0, 255, 65, 0.10)",

            HoverOpacity = 0.08,

            GrayDefault = "#73ff94",
            GrayLight = "#b8ffbf",
            GrayLighter = "#e8ffe9",
            GrayDark = "#2e8f49",
            GrayDarker = "#0d3516"
        };
    }

    private static Typography CreateTypography()
    {
        var monoFamily = new[]
        {
            "Geist Mono",
            "Microsoft YaHei UI",
            "PingFang SC",
            "SFMono-Regular",
            "Consolas",
            "Liberation Mono",
            "Courier New",
            "monospace"
        };

        return new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = monoFamily,
                FontSize = "0.95rem",
                FontWeight = "500",
                LineHeight = "1.55",
                LetterSpacing = "0"
            },
            H1 = new H1Typography
            {
                FontFamily = monoFamily,
                FontSize = "3rem",
                FontWeight = "900",
                LineHeight = "1",
                LetterSpacing = "-0.04em"
            },
            H2 = new H2Typography
            {
                FontFamily = monoFamily,
                FontSize = "2.5rem",
                FontWeight = "900",
                LineHeight = "1.02",
                LetterSpacing = "-0.04em"
            },
            H3 = new H3Typography
            {
                FontFamily = monoFamily,
                FontSize = "2rem",
                FontWeight = "900",
                LineHeight = "1.04",
                LetterSpacing = "-0.03em"
            },
            H4 = new H4Typography
            {
                FontFamily = monoFamily,
                FontSize = "1.5rem",
                FontWeight = "700",
                LineHeight = "1.12",
                LetterSpacing = "-0.02em"
            },
            H5 = new H5Typography
            {
                FontFamily = monoFamily,
                FontSize = "1.125rem",
                FontWeight = "700",
                LineHeight = "1.24",
                LetterSpacing = "-0.01em"
            },
            H6 = new H6Typography
            {
                FontFamily = monoFamily,
                FontSize = "1rem",
                FontWeight = "700",
                LineHeight = "1.3",
                LetterSpacing = "0"
            },
            Subtitle1 = new Subtitle1Typography
            {
                FontFamily = monoFamily,
                FontSize = "0.95rem",
                FontWeight = "500",
                LineHeight = "1.5",
                LetterSpacing = "0.02em"
            },
            Subtitle2 = new Subtitle2Typography
            {
                FontFamily = monoFamily,
                FontSize = "0.82rem",
                FontWeight = "500",
                LineHeight = "1.45",
                LetterSpacing = "0.04em"
            },
            Body1 = new Body1Typography
            {
                FontFamily = monoFamily,
                FontSize = "0.95rem",
                FontWeight = "500",
                LineHeight = "1.6",
                LetterSpacing = "0"
            },
            Body2 = new Body2Typography
            {
                FontFamily = monoFamily,
                FontSize = "0.84rem",
                FontWeight = "500",
                LineHeight = "1.55",
                LetterSpacing = "0.01em"
            },
            Button = new ButtonTypography
            {
                FontFamily = monoFamily,
                FontSize = "0.78rem",
                FontWeight = "700",
                LineHeight = "1.25",
                LetterSpacing = "0.12em",
                TextTransform = "uppercase"
            },
            Caption = new CaptionTypography
            {
                FontFamily = monoFamily,
                FontSize = "0.72rem",
                FontWeight = "500",
                LineHeight = "1.25",
                LetterSpacing = "0.14em",
                TextTransform = "uppercase"
            },
            Overline = new OverlineTypography
            {
                FontFamily = monoFamily,
                FontSize = "0.68rem",
                FontWeight = "700",
                LineHeight = "1.2",
                LetterSpacing = "0.18em",
                TextTransform = "uppercase"
            }
        };
    }

    private static string[] CreateTerminalShadows()
    {
        return
        [
            "none",
            "0 0 0 1px rgba(0, 255, 65, 0.06), 0 1px 3px rgba(0, 0, 0, 0.10)",
            "0 0 0 1px rgba(0, 255, 65, 0.07), 0 2px 6px rgba(0, 0, 0, 0.14)",
            "0 0 0 1px rgba(0, 255, 65, 0.08), 0 4px 10px rgba(0, 0, 0, 0.18)",
            "0 0 0 1px rgba(0, 255, 65, 0.09), 0 6px 14px rgba(0, 0, 0, 0.22)",
            "0 0 0 1px rgba(0, 255, 65, 0.10), 0 8px 18px rgba(0, 0, 0, 0.25)",
            "0 0 0 1px rgba(0, 255, 65, 0.11), 0 10px 22px rgba(0, 0, 0, 0.28)",
            "0 0 0 1px rgba(0, 255, 65, 0.12), 0 12px 26px rgba(0, 0, 0, 0.30)",
            "0 0 0 1px rgba(0, 255, 65, 0.13), 0 14px 30px rgba(0, 0, 0, 0.32)",
            "0 0 0 1px rgba(0, 255, 65, 0.14), 0 16px 34px rgba(0, 0, 0, 0.34)",
            "0 0 0 1px rgba(0, 255, 65, 0.15), 0 18px 38px rgba(0, 0, 0, 0.36)",
            "0 0 0 1px rgba(0, 255, 65, 0.16), 0 20px 42px rgba(0, 0, 0, 0.38)",
            "0 0 0 1px rgba(0, 255, 65, 0.17), 0 22px 46px rgba(0, 0, 0, 0.40)",
            "0 0 0 1px rgba(0, 255, 65, 0.18), 0 24px 50px rgba(0, 0, 0, 0.42)",
            "0 0 0 1px rgba(0, 255, 65, 0.19), 0 26px 54px rgba(0, 0, 0, 0.44)",
            "0 0 0 1px rgba(0, 255, 65, 0.20), 0 28px 58px rgba(0, 0, 0, 0.46)",
            "0 0 0 1px rgba(0, 255, 65, 0.21), 0 30px 62px rgba(0, 0, 0, 0.48)",
            "0 0 0 1px rgba(0, 255, 65, 0.22), 0 32px 66px rgba(0, 0, 0, 0.50)",
            "0 0 0 1px rgba(0, 255, 65, 0.23), 0 34px 70px rgba(0, 0, 0, 0.52)",
            "0 0 0 1px rgba(0, 255, 65, 0.24), 0 36px 74px rgba(0, 0, 0, 0.54)",
            "0 0 0 1px rgba(0, 255, 65, 0.25), 0 38px 78px rgba(0, 0, 0, 0.56)",
            "0 0 0 1px rgba(0, 255, 65, 0.26), 0 40px 82px rgba(0, 0, 0, 0.58)",
            "0 0 0 1px rgba(0, 255, 65, 0.27), 0 42px 86px rgba(0, 0, 0, 0.60)",
            "0 0 0 1px rgba(0, 255, 65, 0.28), 0 44px 90px rgba(0, 0, 0, 0.62)",
            "0 0 0 1px rgba(0, 255, 65, 0.29), 0 46px 94px rgba(0, 0, 0, 0.64)",
            "0 0 0 1px rgba(0, 255, 65, 0.30), 0 48px 98px rgba(0, 0, 0, 0.66)"
        ];
    }
}
