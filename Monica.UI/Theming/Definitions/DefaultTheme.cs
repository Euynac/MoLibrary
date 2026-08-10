using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// The default Monica Precision theme for vivid, information-dense operational interfaces.
/// </summary>
public sealed class DefaultTheme : ThemeDefinitionBase
{
    public override MonicaThemeKind Kind => MonicaThemeKind.Default;
    public override string DisplayName => "默认主题";
    public override string Description => "以鲜明信号色、技术字体和清晰信息层次构建的精密运维界面";

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
        Typography = MonicaTypographyDefaults.CreatePrecision()
    };

    private static PaletteLight CreateLightPalette() => new()
    {
        Primary = "#7257f5",
        PrimaryLighten = "#8b74ff",
        PrimaryDarken = "#5841d2",
        PrimaryContrastText = "#ffffff",

        Secondary = "#526f89",
        SecondaryLighten = "#718ca5",
        SecondaryDarken = "#3d566d",
        SecondaryContrastText = "#ffffff",

        Tertiary = "#eeeafd",
        TertiaryContrastText = "#4e3cb2",

        Info = "#118fc9",
        InfoLighten = "#3baddb",
        InfoDarken = "#0b70a0",
        InfoContrastText = "#ffffff",

        Success = "#0b9f73",
        SuccessLighten = "#2db88d",
        SuccessDarken = "#087d5a",
        SuccessContrastText = "#ffffff",

        Warning = "#b97806",
        WarningLighten = "#d89525",
        WarningDarken = "#925e05",
        WarningContrastText = "#ffffff",

        Error = "#d5445e",
        ErrorLighten = "#e86b82",
        ErrorDarken = "#b1324b",
        ErrorContrastText = "#ffffff",

        Dark = "#151b2b",
        DarkLighten = "#293247",
        DarkDarken = "#0d1220",
        DarkContrastText = "#f4f6fb",

        Background = "#f1f3f7",
        BackgroundGray = "#eef1f6",
        Surface = "#ffffff",

        DrawerBackground = "#ffffff",
        DrawerText = "#516078",
        DrawerIcon = "#68768c",

        AppbarBackground = "rgba(255, 255, 255, 0.94)",
        AppbarText = "#151b2b",

        TextPrimary = "#151b2b",
        TextSecondary = "#516078",
        TextDisabled = "#7f8ca2",

        ActionDefault = "#68768c",
        ActionDisabled = "#aeb7c6",
        ActionDisabledBackground = "#e8ecf2",

        Divider = "#d9deea",
        DividerLight = "#e8ebf1",
        LinesDefault = "#d9deea",
        LinesInputs = "#c3cada",

        TableStriped = "#f8f9fb",
        TableHover = "#f5f3ff",

        OverlayDark = "rgba(21, 27, 43, 0.58)",
        OverlayLight = "rgba(255, 255, 255, 0.78)",
        HoverOpacity = 0.055,

        GrayDefault = "#7f8ca2",
        GrayLight = "#c3cada",
        GrayLighter = "#eef1f6",
        GrayDark = "#516078",
        GrayDarker = "#303a4d"
    };

    private static PaletteDark CreateDarkPalette() => new()
    {
        Primary = "#8c75ff",
        PrimaryLighten = "#a995ff",
        PrimaryDarken = "#7259eb",
        PrimaryContrastText = "#17122b",

        Secondary = "#8fa9c2",
        SecondaryLighten = "#aec3d6",
        SecondaryDarken = "#6d8aa4",
        SecondaryContrastText = "#101720",

        Tertiary = "#292348",
        TertiaryContrastText = "#ddd7ff",

        Info = "#39bdf6",
        InfoLighten = "#6bcdf9",
        InfoDarken = "#169bd4",
        InfoContrastText = "#071820",

        Success = "#34d5a2",
        SuccessLighten = "#64e2ba",
        SuccessDarken = "#17b786",
        SuccessContrastText = "#071b14",

        Warning = "#f1b84b",
        WarningLighten = "#f7cc75",
        WarningDarken = "#d4972e",
        WarningContrastText = "#231706",

        Error = "#ff7189",
        ErrorLighten = "#ff98a9",
        ErrorDarken = "#df506c",
        ErrorContrastText = "#26090f",

        Dark = "#111a29",
        DarkLighten = "#202c40",
        DarkDarken = "#090e18",
        DarkContrastText = "#edf2fb",

        Background = "#0b101a",
        BackgroundGray = "#1b2638",
        Surface = "#141d2c",

        DrawerBackground = "#101724",
        DrawerText = "#dce4f0",
        DrawerIcon = "#9eabc0",

        AppbarBackground = "rgba(11, 16, 26, 0.94)",
        AppbarText = "#edf2fb",

        TextPrimary = "#edf2fb",
        TextSecondary = "#a9b6ca",
        TextDisabled = "#77859b",

        ActionDefault = "#9eabc0",
        ActionDisabled = "#59667a",
        ActionDisabledBackground = "#202c40",

        Divider = "#293449",
        DividerLight = "#222d40",
        LinesDefault = "#293449",
        LinesInputs = "#3a4860",

        TableStriped = "#101724",
        TableHover = "#211f42",

        OverlayDark = "rgba(0, 0, 0, 0.78)",
        OverlayLight = "rgba(20, 29, 44, 0.72)",
        HoverOpacity = 0.075,

        GrayDefault = "#77859b",
        GrayLight = "#9eabc0",
        GrayLighter = "#d0d9e6",
        GrayDark = "#526078",
        GrayDarker = "#303d52"
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

}
