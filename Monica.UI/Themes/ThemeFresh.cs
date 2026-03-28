using MudBlazor;

namespace Monica.UI.Themes;

/// <summary>
/// Small fresh theme: refreshing, simple and soft color matching
/// Inspired by spring’s natural colors and modern minimalist design
/// </summary>
public class ThemeFresh : ThemeBase
{
    public override string Name => "fresh";
    public override string DisplayName => "小清新";
    public override string Description => "清爽柔和的主题，给人以舒适宁静的感觉";
    
    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Googlecode;
    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.AtomOneDark;

    public override MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                // Main color: mint green
                Primary = "#00c896",
                PrimaryLighten = "#33d4aa",
                PrimaryDarken = "#00a87d",
                PrimaryContrastText = "#ffffff",

                // Secondary color: soft coral pink
                Secondary = "#ff8a95",
                SecondaryLighten = "#ffb3ba",
                SecondaryDarken = "#ff6b78",
                SecondaryContrastText = "#ffffff",

                // Third color: sky blue
                Tertiary = "#85d7ff",
                TertiaryContrastText = "#1e5266",

                // Information color: fresh blue
                Info = "#64b5f6",
                InfoLighten = "#90caf9",
                InfoDarken = "#42a5f5",
                InfoContrastText = "#ffffff",

                // Success color: fresh green
                Success = "#66bb6a",
                SuccessLighten = "#81c784",
                SuccessDarken = "#4caf50",
                SuccessContrastText = "#ffffff",

                // Warning Color: Soft Orange
                Warning = "#ffb74d",
                WarningLighten = "#ffcc80",
                WarningDarken = "#ffa726",
                WarningContrastText = "#1e1e1e",

                // Wrong color: soft red
                Error = "#ff7043",
                ErrorLighten = "#ff8a65",
                ErrorDarken = "#f4511e",
                ErrorContrastText = "#ffffff",

                // Dark tones
                Dark = "#424242",
                DarkLighten = "#616161",
                DarkDarken = "#212121",
                DarkContrastText = "#ffffff",

                // Background color: very light mint tone
                Background = "#f8fffe",
                BackgroundGray = "#f5f7f7",

                // Surface color: pure white with a little mint
                Surface = "#ffffff",
                
                // Drawer background
                DrawerBackground = "#fcfffe",
                DrawerText = "#424242",
                DrawerIcon = "#616161",

                // App bar background: fresh white
                AppbarBackground = "#ffffff",
                AppbarText = "#424242",

                // Text color
                TextPrimary = "#2e3440",
                TextSecondary = "#5e6772",
                TextDisabled = "#adb3ba",

                // Operation color
                ActionDefault = "#64b5f6",
                ActionDisabled = "#e0e4e8",
                ActionDisabledBackground = "#f5f7f9",

                // Borders and dividers: very soft gray
                Divider = "#e8ecef",
                DividerLight = "#f0f3f5",

                // Form stripes
                TableStriped = "#fafbfb",
                TableHover = "#f0f8f5",

                // Line
                LinesDefault = "#e0e4e8",
                LinesInputs = "#d0d5da",

                // Covering layer
                OverlayDark = "rgba(33,33,33,0.3)",
                OverlayLight = "rgba(255,255,255,0.7)",

                // Hover state
                HoverOpacity = 0.08,

                // Other
                GrayDefault = "#9e9e9e",
                GrayLight = "#bdbdbd",
                GrayLighter = "#e0e0e0",
                GrayDark = "#757575",
                GrayDarker = "#616161"
            },
            PaletteDark = new PaletteDark()
            {
                // Main color: dark mint green
                Primary = "#00e5a0",
                PrimaryLighten = "#33eab3",
                PrimaryDarken = "#00c586",
                PrimaryContrastText = "#000000",

                // Secondary color: deep coral pink
                Secondary = "#ff9fa8",
                SecondaryLighten = "#ffb8bf",
                SecondaryDarken = "#ff8691",
                SecondaryContrastText = "#000000",

                // Third color: deep sky blue
                Tertiary = "#9ae3ff",
                TertiaryContrastText = "#003548",

                // Information color
                Info = "#81d4fa",
                InfoLighten = "#a1defc",
                InfoDarken = "#4fc3f7",
                InfoContrastText = "#000000",

                // Success color
                Success = "#81c784",
                SuccessLighten = "#a5d6a7",
                SuccessDarken = "#66bb6a",
                SuccessContrastText = "#000000",

                // Warning color
                Warning = "#ffcc80",
                WarningLighten = "#ffd699",
                WarningDarken = "#ffb74d",
                WarningContrastText = "#000000",

                // Wrong color
                Error = "#ff8a65",
                ErrorLighten = "#ffab91",
                ErrorDarken = "#ff7043",
                ErrorContrastText = "#000000",

                // Dark tones
                Dark = "#d0d0d0",
                DarkLighten = "#e0e0e0",
                DarkDarken = "#b0b0b0",
                DarkContrastText = "#000000",

                // Background color: dark with a little green tint
                Background = "#0f1614",
                BackgroundGray = "#141a18",

                // Surface color: dark surface
                Surface = "#1a211f",
                
                // Drawer background
                DrawerBackground = "#161d1b",
                DrawerText = "#e0e0e0",
                DrawerIcon = "#bdbdbd",

                // App bar background
                AppbarBackground = "#1a211f",
                AppbarText = "#e0e0e0",

                // Text color
                TextPrimary = "#eceff1",
                TextSecondary = "#b0bec5",
                TextDisabled = "#607d8b",

                // Operation color
                ActionDefault = "#81d4fa",
                ActionDisabled = "#455a64",
                ActionDisabledBackground = "#263238",

                // Borders and dividing lines
                Divider = "#2a3330",
                DividerLight = "#323b38",

                // Form stripes
                TableStriped = "#1e2624",
                TableHover = "#232b29",

                // Line
                LinesDefault = "#3a4340",
                LinesInputs = "#455a64",

                // Covering layer
                OverlayDark = "rgba(0,0,0,0.5)",
                OverlayLight = "rgba(255,255,255,0.1)",

                // Hover state
                HoverOpacity = 0.12,

                // Other
                GrayDefault = "#9e9e9e",
                GrayLight = "#bdbdbd",
                GrayLighter = "#e0e0e0",
                GrayDark = "#757575",
                GrayDarker = "#616161"
            },
           
            LayoutProperties = new LayoutProperties()
            {
                // Use larger rounded corners to create a softer feel
                DefaultBorderRadius = "12px",
                
                // Drawer width
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "260px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px",
                
                // App bar height
                AppbarHeight = "64px",
            },
            Shadows = new Shadow()
            {
                Elevation = new[]
                {
                    "none",
                    "0 1px 3px rgba(0,0,0,0.05), 0 1px 2px rgba(0,0,0,0.08)",
                    "0 2px 4px rgba(0,0,0,0.05), 0 1px 2px rgba(0,0,0,0.08)",
                    "0 3px 6px rgba(0,0,0,0.05), 0 2px 4px rgba(0,0,0,0.08)",
                    "0 4px 8px rgba(0,0,0,0.05), 0 3px 6px rgba(0,0,0,0.08)",
                    "0 5px 10px rgba(0,0,0,0.05), 0 4px 8px rgba(0,0,0,0.08)",
                    "0 6px 12px rgba(0,0,0,0.05), 0 5px 10px rgba(0,0,0,0.08)",
                    "0 7px 14px rgba(0,0,0,0.05), 0 6px 12px rgba(0,0,0,0.08)",
                    "0 8px 16px rgba(0,0,0,0.05), 0 7px 14px rgba(0,0,0,0.08)",
                    "0 9px 18px rgba(0,0,0,0.05), 0 8px 16px rgba(0,0,0,0.08)",
                    "0 10px 20px rgba(0,0,0,0.05), 0 9px 18px rgba(0,0,0,0.08)",
                    "0 11px 22px rgba(0,0,0,0.05), 0 10px 20px rgba(0,0,0,0.08)",
                    "0 12px 24px rgba(0,0,0,0.05), 0 11px 22px rgba(0,0,0,0.08)",
                    "0 13px 26px rgba(0,0,0,0.05), 0 12px 24px rgba(0,0,0,0.08)",
                    "0 14px 28px rgba(0,0,0,0.05), 0 13px 26px rgba(0,0,0,0.08)",
                    "0 15px 30px rgba(0,0,0,0.05), 0 14px 28px rgba(0,0,0,0.08)",
                    "0 16px 32px rgba(0,0,0,0.05), 0 15px 30px rgba(0,0,0,0.08)",
                    "0 17px 34px rgba(0,0,0,0.05), 0 16px 32px rgba(0,0,0,0.08)",
                    "0 18px 36px rgba(0,0,0,0.05), 0 17px 34px rgba(0,0,0,0.08)",
                    "0 19px 38px rgba(0,0,0,0.05), 0 18px 36px rgba(0,0,0,0.08)",
                    "0 20px 40px rgba(0,0,0,0.05), 0 19px 38px rgba(0,0,0,0.08)",
                    "0 21px 42px rgba(0,0,0,0.05), 0 20px 40px rgba(0,0,0,0.08)",
                    "0 22px 44px rgba(0,0,0,0.05), 0 21px 42px rgba(0,0,0,0.08)",
                    "0 23px 46px rgba(0,0,0,0.05), 0 22px 44px rgba(0,0,0,0.08)",
                    "0 24px 48px rgba(0,0,0,0.05), 0 23px 46px rgba(0,0,0,0.08)",
                    "0 25px 50px rgba(0,0,0,0.05), 0 24px 48px rgba(0,0,0,0.08)"
                }
            },
            ZIndex = new ZIndex()
            {
                Drawer = 1100,
                AppBar = 1200,
                Dialog = 1300,
                Popover = 1400,
                Snackbar = 1500,
                Tooltip = 1600
            }
        };
    }
}
