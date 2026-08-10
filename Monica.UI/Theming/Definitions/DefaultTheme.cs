using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// The default Monica Precision theme for dense operational and developer-facing interfaces.
/// </summary>
public sealed class DefaultTheme : ThemeDefinitionBase
{
    public override MonicaThemeKind Kind => MonicaThemeKind.Default;
    public override string DisplayName => "默认主题";
    public override string Description => "以冷静中性色、清晰边界和克制层次构建的精密运维界面";

    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;
    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.GithubDark;

    public override MudTheme CreateTheme() => new()
    {
        PaletteLight = CreateLightPalette(),
        PaletteDark = CreateDarkPalette(),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "260px",
            DrawerMiniWidthLeft = "64px",
            DrawerMiniWidthRight = "64px",
            AppbarHeight = "56px"
        },
        Shadows = new Shadow { Elevation = CreateElevationScale() },
        Typography = CreateTypography()
    };

    private static PaletteLight CreateLightPalette() => new()
    {
        Primary = "#6657d9",
        PrimaryLighten = "#8377e6",
        PrimaryDarken = "#5042bc",
        PrimaryContrastText = "#ffffff",

        Secondary = "#526f89",
        SecondaryLighten = "#7590a8",
        SecondaryDarken = "#3e586f",
        SecondaryContrastText = "#ffffff",

        Tertiary = "#e9edf3",
        TertiaryContrastText = "#303949",

        Info = "#1188b8",
        InfoLighten = "#3da5cc",
        InfoDarken = "#0b6c94",
        InfoContrastText = "#ffffff",

        Success = "#0a8f69",
        SuccessLighten = "#2aaa82",
        SuccessDarken = "#087254",
        SuccessContrastText = "#ffffff",

        Warning = "#b86f08",
        WarningLighten = "#d58d2a",
        WarningDarken = "#925706",
        WarningContrastText = "#ffffff",

        Error = "#d0445d",
        ErrorLighten = "#e36a7e",
        ErrorDarken = "#ad3049",
        ErrorContrastText = "#ffffff",

        Dark = "#171d2a",
        DarkLighten = "#293244",
        DarkDarken = "#0d111a",
        DarkContrastText = "#f7f9fc",

        Background = "#f3f5f8",
        BackgroundGray = "#e9edf3",
        Surface = "#fcfdff",

        DrawerBackground = "#fcfdff",
        DrawerText = "#4f5b6d",
        DrawerIcon = "#687587",

        AppbarBackground = "rgba(252, 253, 255, 0.94)",
        AppbarText = "#303949",

        TextPrimary = "#181d29",
        TextSecondary = "#5d6879",
        TextDisabled = "#919aaa",

        ActionDefault = "#687587",
        ActionDisabled = "#b8bfca",
        ActionDisabledBackground = "#e9edf3",

        Divider = "#d7dce5",
        DividerLight = "#e7eaf0",
        LinesDefault = "#d7dce5",
        LinesInputs = "#bec6d2",

        TableStriped = "#f6f7fa",
        TableHover = "#edf0f5",

        OverlayDark = "rgba(13, 17, 26, 0.56)",
        OverlayLight = "rgba(252, 253, 255, 0.76)",
        HoverOpacity = 0.055,

        GrayDefault = "#919aaa",
        GrayLight = "#bec6d2",
        GrayLighter = "#e9edf3",
        GrayDark = "#4f5b6d",
        GrayDarker = "#303949"
    };

    private static PaletteDark CreateDarkPalette() => new()
    {
        Primary = "#a695ff",
        PrimaryLighten = "#bcaeff",
        PrimaryDarken = "#8773eb",
        PrimaryContrastText = "#17122b",

        Secondary = "#8aa6bf",
        SecondaryLighten = "#a9bfd2",
        SecondaryDarken = "#68859f",
        SecondaryContrastText = "#101720",

        Tertiary = "#202a39",
        TertiaryContrastText = "#dce4ef",

        Info = "#4cbbe7",
        InfoLighten = "#78cbed",
        InfoDarken = "#299bc7",
        InfoContrastText = "#071820",

        Success = "#35c696",
        SuccessLighten = "#63d5ad",
        SuccessDarken = "#1ba878",
        SuccessContrastText = "#071b14",

        Warning = "#e7aa45",
        WarningLighten = "#f0c271",
        WarningDarken = "#c88a27",
        WarningContrastText = "#231706",

        Error = "#f06f85",
        ErrorLighten = "#f494a4",
        ErrorDarken = "#d34d68",
        ErrorContrastText = "#26090f",

        Dark = "#080d15",
        DarkLighten = "#161f2c",
        DarkDarken = "#04070c",
        DarkContrastText = "#edf2fa",

        Background = "#0c111a",
        BackgroundGray = "#111925",
        Surface = "#161e2b",

        DrawerBackground = "#111925",
        DrawerText = "#d8e0eb",
        DrawerIcon = "#9ca9bb",

        AppbarBackground = "rgba(12, 17, 26, 0.94)",
        AppbarText = "#eef3fb",

        TextPrimary = "#edf2fa",
        TextSecondary = "#a5b0c1",
        TextDisabled = "#687487",

        ActionDefault = "#9ca9bb",
        ActionDisabled = "#4a5668",
        ActionDisabledBackground = "#202a39",

        Divider = "#2a3546",
        DividerLight = "#202a39",
        LinesDefault = "#2a3546",
        LinesInputs = "#3a4659",

        TableStriped = "#111925",
        TableHover = "#1d2735",

        OverlayDark = "rgba(0, 0, 0, 0.78)",
        OverlayLight = "rgba(22, 30, 43, 0.72)",
        HoverOpacity = 0.075,

        GrayDefault = "#788598",
        GrayLight = "#9ca9bb",
        GrayLighter = "#cad3df",
        GrayDark = "#4a5668",
        GrayDarker = "#2f3b4d"
    };

    private static string[] CreateElevationScale() =>
    [
        "none",
        "0 1px 2px rgba(10, 16, 28, 0.06)",
        "0 2px 6px -1px rgba(10, 16, 28, 0.08)",
        "0 4px 10px -2px rgba(10, 16, 28, 0.09)",
        "0 6px 14px -3px rgba(10, 16, 28, 0.10)",
        "0 8px 18px -4px rgba(10, 16, 28, 0.11)",
        "0 10px 22px -5px rgba(10, 16, 28, 0.12)",
        "0 12px 26px -6px rgba(10, 16, 28, 0.13)",
        "0 14px 30px -7px rgba(10, 16, 28, 0.14)",
        "0 16px 34px -8px rgba(10, 16, 28, 0.15)",
        "0 18px 38px -9px rgba(10, 16, 28, 0.16)",
        "0 20px 42px -10px rgba(10, 16, 28, 0.17)",
        "0 22px 46px -11px rgba(10, 16, 28, 0.18)",
        "0 24px 50px -12px rgba(10, 16, 28, 0.19)",
        "0 24px 54px -12px rgba(10, 16, 28, 0.20)",
        "0 26px 58px -13px rgba(10, 16, 28, 0.21)",
        "0 26px 62px -13px rgba(10, 16, 28, 0.22)",
        "0 28px 64px -14px rgba(10, 16, 28, 0.23)",
        "0 28px 68px -14px rgba(10, 16, 28, 0.24)",
        "0 30px 70px -15px rgba(10, 16, 28, 0.25)",
        "0 30px 72px -15px rgba(10, 16, 28, 0.26)",
        "0 30px 74px -15px rgba(10, 16, 28, 0.27)",
        "0 30px 76px -16px rgba(10, 16, 28, 0.28)",
        "0 30px 78px -16px rgba(10, 16, 28, 0.29)",
        "0 30px 80px -16px rgba(10, 16, 28, 0.30)",
        "0 32px 84px -17px rgba(10, 16, 28, 0.32)"
    ];

    private static Typography CreateTypography() => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["MoDefaultText", "Source Sans Pro", "Segoe UI", "Noto Sans CJK SC", "sans-serif"],
            FontSize = "0.9375rem",
            FontWeight = "400",
            LineHeight = "1.5",
            LetterSpacing = "0"
        },
        H1 = new H1Typography { FontSize = "2.5rem", FontWeight = "600", LineHeight = "1.08", LetterSpacing = "-0.035em" },
        H2 = new H2Typography { FontSize = "2rem", FontWeight = "600", LineHeight = "1.12", LetterSpacing = "-0.03em" },
        H3 = new H3Typography { FontSize = "1.625rem", FontWeight = "600", LineHeight = "1.18", LetterSpacing = "-0.025em" },
        H4 = new H4Typography { FontSize = "1.375rem", FontWeight = "600", LineHeight = "1.25", LetterSpacing = "-0.02em" },
        H5 = new H5Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.35", LetterSpacing = "-0.012em" },
        H6 = new H6Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "-0.006em" },
        Subtitle1 = new Subtitle1Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.5", LetterSpacing = "0" },
        Subtitle2 = new Subtitle2Typography { FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "0" },
        Body1 = new Body1Typography { FontSize = "0.95rem", FontWeight = "400", LineHeight = "1.55", LetterSpacing = "0" },
        Body2 = new Body2Typography { FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "0" },
        Button = new ButtonTypography { FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.25", LetterSpacing = "0", TextTransform = "none" },
        Caption = new CaptionTypography { FontSize = "0.75rem", FontWeight = "400", LineHeight = "1.35", LetterSpacing = "0.01em" },
        Overline = new OverlineTypography { FontSize = "0.6875rem", FontWeight = "600", LineHeight = "1.25", LetterSpacing = "0.09em", TextTransform = "uppercase" }
    };
}
