using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Playful pastel macaron theme with rounded candy surfaces and handwritten typography.
/// </summary>
public sealed class MacaronSweetheartTheme : ThemeDefinitionBase
{
    private static readonly string[] HandwrittenFontFamily =
    [
        "Xiaolai",
        "DynaPuff",
        "Nunito",
        "sans-serif"
    ];

    private static readonly string[] DisplayFontFamily =
    [
        "DynaPuff",
        "Xiaolai",
        "Nunito",
        "sans-serif"
    ];

    public override string Name => "macaron-sweetheart";

    public override string DisplayName => "马卡龙甜心";

    public override string Description => "粉嫩马卡龙色系，圆润俏皮的甜美手写风格";

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
                DefaultBorderRadius = "22px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "276px",
                DrawerWidthRight = "276px",
                DrawerMiniWidthLeft = "80px",
                DrawerMiniWidthRight = "80px"
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
            Primary = "#F48FB1",
            PrimaryLighten = "#FFC4D6",
            PrimaryDarken = "#D86A94",
            PrimaryContrastText = "#4F2A3A",

            Secondary = "#9AD7F5",
            SecondaryLighten = "#C7ECFF",
            SecondaryDarken = "#6FBEE7",
            SecondaryContrastText = "#224558",

            Tertiary = "#B8E8C7",
            TertiaryLighten = "#D9F7E2",
            TertiaryDarken = "#8DD4A3",
            TertiaryContrastText = "#315541",

            Info = "#9AD7F5",
            InfoLighten = "#C7ECFF",
            InfoDarken = "#6FBEE7",
            InfoContrastText = "#224558",

            Success = "#B8E8C7",
            SuccessLighten = "#D9F7E2",
            SuccessDarken = "#8DD4A3",
            SuccessContrastText = "#315541",

            Warning = "#FFD58A",
            WarningLighten = "#FFE8B8",
            WarningDarken = "#F4B85C",
            WarningContrastText = "#60451C",

            Error = "#F47D8A",
            ErrorLighten = "#FFB5BE",
            ErrorDarken = "#D95868",
            ErrorContrastText = "#55262D",

            Dark = "#5D4250",
            DarkLighten = "#7E6271",
            DarkDarken = "#3F2A35",
            DarkContrastText = "#FFF6FA",

            Background = "#FFF6FA",
            BackgroundGray = "#FFEAF3",
            Surface = "#FFFFFF",

            DrawerBackground = "#FFF0F6",
            DrawerText = "#5D4250",
            DrawerIcon = "#A45C7A",

            AppbarBackground = "rgba(255, 246, 250, 0.94)",
            AppbarText = "#5D4250",

            TextPrimary = "#5D4250",
            TextSecondary = "#8A6676",
            TextDisabled = "#C5A1B1",

            ActionDefault = "#A45C7A",
            ActionDisabled = "#D9B7C7",
            ActionDisabledBackground = "#FFEAF3",

            Divider = "rgba(244, 143, 177, 0.24)",
            DividerLight = "rgba(244, 143, 177, 0.14)",
            LinesDefault = "rgba(244, 143, 177, 0.24)",
            LinesInputs = "rgba(244, 143, 177, 0.36)",

            TableLines = "rgba(244, 143, 177, 0.18)",
            TableStriped = "#FFF8FB",
            TableHover = "#FFEAF3",

            OverlayDark = "rgba(93, 66, 80, 0.38)",
            OverlayLight = "rgba(255, 255, 255, 0.78)",

            HoverOpacity = 0.07,

            GrayDefault = "#B28CA0",
            GrayLight = "#D6B8C8",
            GrayLighter = "#F4DDEA",
            GrayDark = "#8A6676",
            GrayDarker = "#5D4250"
        };
    }

    private static PaletteDark CreateDarkPalette()
    {
        return new PaletteDark
        {
            Primary = "#FFB1CA",
            PrimaryLighten = "#FFD2E0",
            PrimaryDarken = "#E889AA",
            PrimaryContrastText = "#4E2235",

            Secondary = "#A9E1FF",
            SecondaryLighten = "#D0F0FF",
            SecondaryDarken = "#82C8EE",
            SecondaryContrastText = "#18384A",

            Tertiary = "#C6F2D4",
            TertiaryLighten = "#E1FAE9",
            TertiaryDarken = "#9CDDB2",
            TertiaryContrastText = "#264934",

            Info = "#A9E1FF",
            InfoLighten = "#D0F0FF",
            InfoDarken = "#82C8EE",
            InfoContrastText = "#18384A",

            Success = "#C6F2D4",
            SuccessLighten = "#E1FAE9",
            SuccessDarken = "#9CDDB2",
            SuccessContrastText = "#264934",

            Warning = "#FFE1A6",
            WarningLighten = "#FFF0CC",
            WarningDarken = "#F0BF72",
            WarningContrastText = "#513817",

            Error = "#FFA0AA",
            ErrorLighten = "#FFC8CE",
            ErrorDarken = "#E57582",
            ErrorContrastText = "#4C1F27",

            Dark = "#FFF1F7",
            DarkLighten = "#FFFFFF",
            DarkDarken = "#EBD0DB",
            DarkContrastText = "#241821",

            Background = "#241821",
            BackgroundGray = "#2B1C28",
            Surface = "#30202C",

            DrawerBackground = "#2A1A26",
            DrawerText = "#FFF1F7",
            DrawerIcon = "#F2B5CC",

            AppbarBackground = "rgba(36, 24, 33, 0.94)",
            AppbarText = "#FFF1F7",

            TextPrimary = "#FFF1F7",
            TextSecondary = "#E8C6D4",
            TextDisabled = "#A98494",

            ActionDefault = "#F2B5CC",
            ActionDisabled = "#785665",
            ActionDisabledBackground = "#3A2734",

            Divider = "rgba(255, 177, 202, 0.22)",
            DividerLight = "rgba(255, 177, 202, 0.12)",
            LinesDefault = "rgba(255, 177, 202, 0.22)",
            LinesInputs = "rgba(255, 177, 202, 0.34)",

            TableLines = "rgba(255, 177, 202, 0.16)",
            TableStriped = "#2A1A26",
            TableHover = "#3A2734",

            OverlayDark = "rgba(12, 7, 11, 0.72)",
            OverlayLight = "rgba(255, 241, 247, 0.12)",

            HoverOpacity = 0.09,

            GrayDefault = "#A98494",
            GrayLight = "#C8A6B5",
            GrayLighter = "#E8C6D4",
            GrayDark = "#785665",
            GrayDarker = "#4D3442"
        };
    }

    private static Typography CreateTypography()
    {
        return new Typography
        {
            Default = CreateTypography<DefaultTypography>(HandwrittenFontFamily, "0.9375rem", "400", "1.65"),
            H1 = CreateTypography<H1Typography>(DisplayFontFamily, "3rem", "600", "1.18"),
            H2 = CreateTypography<H2Typography>(DisplayFontFamily, "2.5rem", "600", "1.22"),
            H3 = CreateTypography<H3Typography>(DisplayFontFamily, "2rem", "600", "1.28"),
            H4 = CreateTypography<H4Typography>(DisplayFontFamily, "1.625rem", "600", "1.35"),
            H5 = CreateTypography<H5Typography>(DisplayFontFamily, "1.375rem", "500", "1.42"),
            H6 = CreateTypography<H6Typography>(DisplayFontFamily, "1.125rem", "500", "1.5"),
            Subtitle1 = CreateTypography<Subtitle1Typography>(DisplayFontFamily, "1rem", "500", "1.55"),
            Subtitle2 = CreateTypography<Subtitle2Typography>(HandwrittenFontFamily, "0.9375rem", "500", "1.55"),
            Body1 = CreateTypography<Body1Typography>(HandwrittenFontFamily, "1rem", "400", "1.7"),
            Body2 = CreateTypography<Body2Typography>(HandwrittenFontFamily, "0.875rem", "400", "1.65"),
            Button = CreateButtonTypography(),
            Caption = CreateTypography<CaptionTypography>(HandwrittenFontFamily, "0.75rem", "400", "1.5"),
            Overline = CreateOverlineTypography()
        };
    }

    private static TTypography CreateTypography<TTypography>(
        string[] fontFamily,
        string fontSize,
        string fontWeight,
        string lineHeight)
        where TTypography : BaseTypography, new()
    {
        return new TTypography
        {
            FontFamily = fontFamily,
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
            FontFamily = DisplayFontFamily,
            FontSize = "0.75rem",
            FontWeight = "500",
            LineHeight = "1.5",
            LetterSpacing = "0",
            TextTransform = "none"
        };
    }

    private static string[] CreateShadows()
    {
        return
        [
            "none",
            "0 2px 4px rgba(93, 66, 80, 0.06)",
            "0 4px 10px rgba(244, 143, 177, 0.12)",
            "0 6px 16px rgba(244, 143, 177, 0.14)",
            "0 8px 22px rgba(244, 143, 177, 0.16)",
            "0 10px 28px rgba(244, 143, 177, 0.18)",
            "0 12px 34px rgba(244, 143, 177, 0.20)",
            "0 14px 40px rgba(244, 143, 177, 0.21)",
            "0 16px 46px rgba(244, 143, 177, 0.22)",
            "0 18px 52px rgba(244, 143, 177, 0.23)",
            "0 20px 58px rgba(244, 143, 177, 0.24)",
            "0 22px 64px rgba(244, 143, 177, 0.25)",
            "0 24px 70px rgba(244, 143, 177, 0.26)",
            "0 26px 76px rgba(244, 143, 177, 0.27)",
            "0 28px 82px rgba(244, 143, 177, 0.28)",
            "0 30px 88px rgba(244, 143, 177, 0.29)",
            "0 32px 94px rgba(244, 143, 177, 0.30)",
            "0 34px 100px rgba(244, 143, 177, 0.31)",
            "0 36px 106px rgba(244, 143, 177, 0.32)",
            "0 38px 112px rgba(244, 143, 177, 0.33)",
            "0 40px 118px rgba(244, 143, 177, 0.34)",
            "0 42px 124px rgba(244, 143, 177, 0.35)",
            "0 44px 130px rgba(244, 143, 177, 0.36)",
            "0 46px 136px rgba(244, 143, 177, 0.37)",
            "0 48px 142px rgba(244, 143, 177, 0.38)",
            "0 50px 148px rgba(244, 143, 177, 0.39)"
        ];
    }
}
