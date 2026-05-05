using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// Small fresh theme: refreshing, simple and soft color matching
/// Inspired by spring natural colors and modern minimalist design.
/// </summary>
public class FreshTheme : ThemeDefinitionBase
{
    public override MonicaThemeKind Kind => MonicaThemeKind.Fresh;
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
                // Main color: Mint Green (Fresh & Natural)
                Primary = "#66CBA3",
                PrimaryLighten = "#8CE0C0",
                PrimaryDarken = "#46AC82",
                PrimaryContrastText = "#FFFFFF",

                // Secondary color: Serenity Blue (Adding delicate layers)
                Secondary = "#8FABC9",
                SecondaryLighten = "#AEC5EB",
                SecondaryDarken = "#708EA8",
                SecondaryContrastText = "#FFFFFF",

                // Third color: Soft Pink
                Tertiary = "#ECA8BA",
                TertiaryContrastText = "#FFFFFF",

                // Info color: Serenity Blue
                Info = "#8FABC9",
                InfoLighten = "#AEC5EB",
                InfoDarken = "#708EA8",
                InfoContrastText = "#FFFFFF",

                // Success color: Mint Green
                Success = "#66CBA3",
                SuccessLighten = "#8CE0C0",
                SuccessDarken = "#46AC82",
                SuccessContrastText = "#FFFFFF",

                // Warning Color: Soft Orange
                Warning = "#E8BC71",
                WarningLighten = "#F6D194",
                WarningDarken = "#CD9F53",
                WarningContrastText = "#FFFFFF",

                // Error color: Soft Red
                Error = "#DF8479",
                ErrorLighten = "#F09C92",
                ErrorDarken = "#C2675C",
                ErrorContrastText = "#FFFFFF",

                // Dark tones
                Dark = "#4B5563",
                DarkLighten = "#6B7280",
                DarkDarken = "#374151",
                DarkContrastText = "#FFFFFF",

                // Background color: Off-White/Creamy Beige
                Background = "#F9F8F6",
                BackgroundGray = "#F1EFEA",

                // Surface color: Pure white
                Surface = "#FFFFFF",
                
                // Drawer background
                DrawerBackground = "#F9F8F6",
                DrawerText = "#4B5563",
                DrawerIcon = "#6B7280",

                // App bar background
                AppbarBackground = "#FFFFFF",
                AppbarText = "#4B5563",

                // Text color
                TextPrimary = "#374151",
                TextSecondary = "#6B7280",
                TextDisabled = "#9CA3AF",

                // Operation color
                ActionDefault = "#8FABC9",
                ActionDisabled = "#E5E7EB",
                ActionDisabledBackground = "#F3F4F6",

                // Borders and dividers
                Divider = "#E5E7EB",
                DividerLight = "#F3F4F6",

                // Form stripes
                TableStriped = "#F9F8F6",
                TableHover = "#F1EFEA",

                // Line
                LinesDefault = "#E5E7EB",
                LinesInputs = "#D1D5DB",

                // Covering layer
                OverlayDark = "rgba(55,65,81,0.4)",
                OverlayLight = "rgba(255,255,255,0.7)",

                // Hover state
                HoverOpacity = 0.06,

                // Other
                GrayDefault = "#9CA3AF",
                GrayLight = "#D1D5DB",
                GrayLighter = "#E5E7EB",
                GrayDark = "#6B7280",
                GrayDarker = "#4B5563"
            },
            PaletteDark = new PaletteDark()
            {
                // Main color: Mint Green
                Primary = "#7CE3B9",
                PrimaryLighten = "#9DF2CE",
                PrimaryDarken = "#56C498",
                PrimaryContrastText = "#1A1D1C",

                // Secondary color: Serenity Blue
                Secondary = "#A8C2E0",
                SecondaryLighten = "#C6DCF4",
                SecondaryDarken = "#86A5C8",
                SecondaryContrastText = "#1A1D1C",

                // Third color: Soft Pink
                Tertiary = "#F2B8CA",
                TertiaryContrastText = "#1A1D1C",

                // Info color
                Info = "#A8C2E0",
                InfoLighten = "#C6DCF4",
                InfoDarken = "#86A5C8",
                InfoContrastText = "#1A1D1C",

                // Success color
                Success = "#7CE3B9",
                SuccessLighten = "#9DF2CE",
                SuccessDarken = "#56C498",
                SuccessContrastText = "#1A1D1C",

                // Warning color
                Warning = "#F0CA8D",
                WarningLighten = "#FBE0AD",
                WarningDarken = "#D6AB6D",
                WarningContrastText = "#1A1D1C",

                // Error color
                Error = "#EE9D92",
                ErrorLighten = "#FBB7AE",
                ErrorDarken = "#D17D71",
                ErrorContrastText = "#1A1D1C",

                // Dark tones
                Dark = "#D1D5DB",
                DarkLighten = "#E5E7EB",
                DarkDarken = "#9CA3AF",
                DarkContrastText = "#1A1D1C",

                // Background color: Dark Mint Tone
                Background = "#1A1D1C",
                BackgroundGray = "#151817",

                // Surface color
                Surface = "#212524",
                
                // Drawer background
                DrawerBackground = "#1C201F",
                DrawerText = "#D1D5DB",
                DrawerIcon = "#9CA3AF",

                // App bar background
                AppbarBackground = "#212524",
                AppbarText = "#D1D5DB",

                // Text color
                TextPrimary = "#F3F4F6",
                TextSecondary = "#9CA3AF",
                TextDisabled = "#6B7280",

                // Operation color
                ActionDefault = "#A8C2E0",
                ActionDisabled = "#4B5563",
                ActionDisabledBackground = "#374151",

                // Borders and dividing lines
                Divider = "#374151",
                DividerLight = "#4B5563",

                // Form stripes
                TableStriped = "#1C201F",
                TableHover = "#282C2B",

                // Line
                LinesDefault = "#374151",
                LinesInputs = "#4B5563",

                // Covering layer
                OverlayDark = "rgba(0,0,0,0.6)",
                OverlayLight = "rgba(255,255,255,0.1)",

                // Hover state
                HoverOpacity = 0.1,

                // Other
                GrayDefault = "#9CA3AF",
                GrayLight = "#D1D5DB",
                GrayLighter = "#E5E7EB",
                GrayDark = "#6B7280",
                GrayDarker = "#4B5563"
            },
            LayoutProperties = new LayoutProperties()
            {
                // Soft rounded corners
                DefaultBorderRadius = "12px",
                
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "260px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px",
                
                AppbarHeight = "64px",
            },
            Shadows = new Shadow()
            {
                Elevation = new[]
                {
                    "none",
                    "0 1px 3px rgba(0,0,0,0.03), 0 1px 2px rgba(0,0,0,0.05)",
                    "0 2px 4px rgba(0,0,0,0.03), 0 1px 2px rgba(0,0,0,0.05)",
                    "0 3px 6px rgba(0,0,0,0.03), 0 2px 4px rgba(0,0,0,0.05)",
                    "0 4px 8px rgba(0,0,0,0.03), 0 3px 6px rgba(0,0,0,0.05)",
                    "0 5px 10px rgba(0,0,0,0.03), 0 4px 8px rgba(0,0,0,0.05)",
                    "0 6px 12px rgba(0,0,0,0.03), 0 5px 10px rgba(0,0,0,0.05)",
                    "0 7px 14px rgba(0,0,0,0.03), 0 6px 12px rgba(0,0,0,0.05)",
                    "0 8px 16px rgba(0,0,0,0.03), 0 7px 14px rgba(0,0,0,0.05)",
                    "0 9px 18px rgba(0,0,0,0.03), 0 8px 16px rgba(0,0,0,0.05)",
                    "0 10px 20px rgba(0,0,0,0.03), 0 9px 18px rgba(0,0,0,0.05)",
                    "0 11px 22px rgba(0,0,0,0.03), 0 10px 20px rgba(0,0,0,0.05)",
                    "0 12px 24px rgba(0,0,0,0.03), 0 11px 22px rgba(0,0,0,0.05)",
                    "0 13px 26px rgba(0,0,0,0.03), 0 12px 24px rgba(0,0,0,0.05)",
                    "0 14px 28px rgba(0,0,0,0.03), 0 13px 26px rgba(0,0,0,0.05)",
                    "0 15px 30px rgba(0,0,0,0.03), 0 14px 28px rgba(0,0,0,0.05)",
                    "0 16px 32px rgba(0,0,0,0.03), 0 15px 30px rgba(0,0,0,0.05)",
                    "0 17px 34px rgba(0,0,0,0.03), 0 16px 32px rgba(0,0,0,0.05)",
                    "0 18px 36px rgba(0,0,0,0.03), 0 17px 34px rgba(0,0,0,0.05)",
                    "0 19px 38px rgba(0,0,0,0.03), 0 18px 36px rgba(0,0,0,0.05)",
                    "0 20px 40px rgba(0,0,0,0.03), 0 19px 38px rgba(0,0,0,0.05)",
                    "0 21px 42px rgba(0,0,0,0.03), 0 20px 40px rgba(0,0,0,0.05)",
                    "0 22px 44px rgba(0,0,0,0.03), 0 21px 42px rgba(0,0,0,0.05)",
                    "0 23px 46px rgba(0,0,0,0.03), 0 22px 44px rgba(0,0,0,0.05)",
                    "0 24px 48px rgba(0,0,0,0.03), 0 23px 46px rgba(0,0,0,0.05)",
                    "0 25px 50px rgba(0,0,0,0.03), 0 24px 48px rgba(0,0,0,0.05)"
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
