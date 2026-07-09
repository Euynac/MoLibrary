using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Inked comic theme inspired by Komi Store's manga-style product UI,
/// using print-paper surfaces, hard ink borders, and saturated accent fills.
/// </summary>
public sealed class ComicBurstTheme : ThemeDefinitionBase
{
    private static readonly string[] DisplayFontFamily =
    [
        "Komi Manga Display",
        "Anton",
        "ZCOOL QingKe HuangYou",
        "Microsoft YaHei UI",
        "PingFang SC",
        "Hiragino Sans GB",
        "Source Han Sans SC",
        "WenQuanYi Micro Hei",
        "sans-serif"
    ];

    private static readonly string[] BodyFontFamily =
    [
        "Microsoft YaHei UI",
        "PingFang SC",
        "Hiragino Sans GB",
        "Source Han Sans SC",
        "WenQuanYi Micro Hei",
        "system-ui",
        "sans-serif"
    ];

    private static readonly string[] MonoFontFamily =
    [
        "Komi Manga Mono",
        "JetBrains Mono",
        "Geist Mono",
        "SFMono-Regular",
        "Consolas",
        "Liberation Mono",
        "monospace"
    ];

    public override MonicaThemeKind Kind => MonicaThemeKind.ComicBurst;

    public override string DisplayName => "Comic Burst";

    public override string Description => "Inked comic panels with paper texture, hard shadows, and saturated red-yellow accents.";

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
                DefaultBorderRadius = "0px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px"
            },
            Typography = CreateTypography(),
            Shadows = new Shadow
            {
                Elevation = CreateComicShadows()
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
            Primary = "#D8202A",
            PrimaryLighten = "#F04A54",
            PrimaryDarken = "#A91520",
            PrimaryContrastText = "#FFFFFF",

            Secondary = "#F5A300",
            SecondaryLighten = "#FFC247",
            SecondaryDarken = "#BD7E00",
            SecondaryContrastText = "#1B150D",

            Tertiary = "#1B150D",
            TertiaryLighten = "#3A3024",
            TertiaryDarken = "#0C0804",
            TertiaryContrastText = "#FFF6E3",

            Info = "#0B79A8",
            InfoLighten = "#2AA6D8",
            InfoDarken = "#075A80",
            InfoContrastText = "#FFFFFF",

            Success = "#168848",
            SuccessLighten = "#25B866",
            SuccessDarken = "#0F6234",
            SuccessContrastText = "#FFFFFF",

            Warning = "#F5A300",
            WarningLighten = "#FFC247",
            WarningDarken = "#BD7E00",
            WarningContrastText = "#1B150D",

            Error = "#D8202A",
            ErrorLighten = "#F04A54",
            ErrorDarken = "#A91520",
            ErrorContrastText = "#FFFFFF",

            Dark = "#1B150D",
            DarkLighten = "#3A3024",
            DarkDarken = "#0C0804",
            DarkContrastText = "#FFF6E3",

            Background = "#F1EADC",
            BackgroundGray = "#E7DEC9",
            Surface = "#FAF5EA",

            DrawerBackground = "#FAF5EA",
            DrawerText = "#1B150D",
            DrawerIcon = "#D8202A",

            AppbarBackground = "rgba(250, 245, 234, 0.96)",
            AppbarText = "#1B150D",

            TextPrimary = "#1B150D",
            TextSecondary = "#695F50",
            TextDisabled = "#A79C8B",

            ActionDefault = "#1B150D",
            ActionDisabled = "#A79C8B",
            ActionDisabledBackground = "#E7DEC9",

            Divider = "rgba(27, 21, 13, 0.22)",
            DividerLight = "rgba(27, 21, 13, 0.12)",
            LinesDefault = "rgba(27, 21, 13, 0.22)",
            LinesInputs = "rgba(27, 21, 13, 0.42)",

            TableLines = "rgba(27, 21, 13, 0.18)",
            TableStriped = "#FFF6E3",
            TableHover = "#E7DEC9",

            OverlayDark = "rgba(27, 21, 13, 0.46)",
            OverlayLight = "rgba(255, 246, 227, 0.72)",

            HoverOpacity = 0.08,

            GrayDefault = "#695F50",
            GrayLight = "#BFB29C",
            GrayLighter = "#E7DEC9",
            GrayDark = "#3A3024",
            GrayDarker = "#1B150D"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#FF4A54",
            PrimaryLighten = "#FF757D",
            PrimaryDarken = "#D8202A",
            PrimaryContrastText = "#FFFFFF",

            Secondary = "#FFC247",
            SecondaryLighten = "#FFD77A",
            SecondaryDarken = "#F5A300",
            SecondaryContrastText = "#1B150D",

            Tertiary = "#F0E9DA",
            TertiaryLighten = "#FFF6E3",
            TertiaryDarken = "#CDBFA9",
            TertiaryContrastText = "#0C0A07",

            Info = "#55C7F2",
            InfoLighten = "#8DDDFA",
            InfoDarken = "#1FA2D5",
            InfoContrastText = "#0C0A07",

            Success = "#5FDB91",
            SuccessLighten = "#92ECB4",
            SuccessDarken = "#2FB96C",
            SuccessContrastText = "#0C0A07",

            Warning = "#FFC247",
            WarningLighten = "#FFD77A",
            WarningDarken = "#F5A300",
            WarningContrastText = "#1B150D",

            Error = "#FF6C74",
            ErrorLighten = "#FF9399",
            ErrorDarken = "#E23C46",
            ErrorContrastText = "#0C0A07",

            Dark = "#F0E9DA",
            DarkLighten = "#FFF6E3",
            DarkDarken = "#CDBFA9",
            DarkContrastText = "#0C0A07",

            Background = "#0C0A07",
            BackgroundGray = "#211B12",
            Surface = "#16120C",

            DrawerBackground = "#16120C",
            DrawerText = "#F0E9DA",
            DrawerIcon = "#FF4A54",

            AppbarBackground = "rgba(22, 18, 12, 0.96)",
            AppbarText = "#F0E9DA",

            TextPrimary = "#F0E9DA",
            TextSecondary = "#B9AD98",
            TextDisabled = "#756B5A",

            ActionDefault = "#F0E9DA",
            ActionDisabled = "#756B5A",
            ActionDisabledBackground = "#211B12",

            Divider = "rgba(240, 233, 218, 0.22)",
            DividerLight = "rgba(240, 233, 218, 0.12)",
            LinesDefault = "rgba(240, 233, 218, 0.22)",
            LinesInputs = "rgba(240, 233, 218, 0.40)",

            TableLines = "rgba(240, 233, 218, 0.16)",
            TableStriped = "#1A150F",
            TableHover = "#211B12",

            OverlayDark = "rgba(0, 0, 0, 0.78)",
            OverlayLight = "rgba(240, 233, 218, 0.14)",

            HoverOpacity = 0.10,

            GrayDefault = "#B9AD98",
            GrayLight = "#DED3C0",
            GrayLighter = "#F0E9DA",
            GrayDark = "#756B5A",
            GrayDarker = "#3B3328"
        };
    }

    private static Typography CreateTypography()
    {
        return new Typography
        {
            Default = CreateBodyTypography<DefaultTypography>("0.95rem", "400", "1.65"),
            H1 = CreateDisplayTypography<H1Typography>("3.25rem", "400", "0.98"),
            H2 = CreateDisplayTypography<H2Typography>("2.6rem", "400", "1"),
            H3 = CreateDisplayTypography<H3Typography>("2rem", "400", "1.05"),
            H4 = CreateDisplayTypography<H4Typography>("1.5rem", "400", "1.12"),
            H5 = CreateDisplayTypography<H5Typography>("1.25rem", "400", "1.18"),
            H6 = CreateDisplayTypography<H6Typography>("1.05rem", "400", "1.25"),
            Subtitle1 = CreateBodyTypography<Subtitle1Typography>("1rem", "700", "1.5"),
            Subtitle2 = CreateBodyTypography<Subtitle2Typography>("0.875rem", "700", "1.45"),
            Body1 = CreateBodyTypography<Body1Typography>("1rem", "400", "1.7"),
            Body2 = CreateBodyTypography<Body2Typography>("0.875rem", "400", "1.65"),
            Button = CreateButtonTypography(),
            Caption = CreateBodyTypography<CaptionTypography>("0.75rem", "500", "1.5"),
            Overline = CreateOverlineTypography()
        };
    }

    private static TTypography CreateDisplayTypography<TTypography>(
        string fontSize,
        string fontWeight,
        string lineHeight)
        where TTypography : BaseTypography, new()
    {
        return new TTypography
        {
            FontFamily = DisplayFontFamily,
            FontSize = fontSize,
            FontWeight = fontWeight,
            LineHeight = lineHeight,
            LetterSpacing = "0.01em"
        };
    }

    private static TTypography CreateBodyTypography<TTypography>(
        string fontSize,
        string fontWeight,
        string lineHeight)
        where TTypography : BaseTypography, new()
    {
        return new TTypography
        {
            FontFamily = BodyFontFamily,
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
            FontFamily = DisplayFontFamily,
            FontSize = "0.9rem",
            FontWeight = "400",
            LineHeight = "1.2",
            LetterSpacing = "0.03em",
            TextTransform = "uppercase"
        };
    }

    private static OverlineTypography CreateOverlineTypography()
    {
        return new OverlineTypography
        {
            FontFamily = MonoFontFamily,
            FontSize = "0.72rem",
            FontWeight = "700",
            LineHeight = "1.3",
            LetterSpacing = "0.08em",
            TextTransform = "uppercase"
        };
    }

    private static string[] CreateComicShadows()
    {
        return
        [
            "none",
            "1px 1px 0 rgba(27, 21, 13, 0.95)",
            "2px 2px 0 rgba(27, 21, 13, 0.95)",
            "3px 3px 0 rgba(27, 21, 13, 0.95)",
            "4px 4px 0 rgba(27, 21, 13, 0.95)",
            "5px 5px 0 rgba(27, 21, 13, 0.95)",
            "6px 6px 0 rgba(27, 21, 13, 0.95)",
            "7px 7px 0 rgba(27, 21, 13, 0.95)",
            "8px 8px 0 rgba(27, 21, 13, 0.95)",
            "9px 9px 0 rgba(27, 21, 13, 0.95)",
            "10px 10px 0 rgba(27, 21, 13, 0.95)",
            "11px 11px 0 rgba(27, 21, 13, 0.95)",
            "12px 12px 0 rgba(27, 21, 13, 0.95)",
            "13px 13px 0 rgba(27, 21, 13, 0.95)",
            "14px 14px 0 rgba(27, 21, 13, 0.95)",
            "15px 15px 0 rgba(27, 21, 13, 0.95)",
            "16px 16px 0 rgba(27, 21, 13, 0.95)",
            "17px 17px 0 rgba(27, 21, 13, 0.95)",
            "18px 18px 0 rgba(27, 21, 13, 0.95)",
            "19px 19px 0 rgba(27, 21, 13, 0.95)",
            "20px 20px 0 rgba(27, 21, 13, 0.95)",
            "21px 21px 0 rgba(27, 21, 13, 0.95)",
            "22px 22px 0 rgba(27, 21, 13, 0.95)",
            "23px 23px 0 rgba(27, 21, 13, 0.95)",
            "24px 24px 0 rgba(27, 21, 13, 0.95)",
            "25px 25px 0 rgba(27, 21, 13, 0.95)"
        ];
    }
}
