using MudBlazor;

namespace Monica.UI.Theming.Definitions;

/// <summary>
/// A modern, SaaS-like clean theme with soft borders, high contrast, and a diffuse shadow system.
/// </summary>
public class CleanSaaSTheme : ThemeDefinitionBase
{
    public override string Name => "clean-saas";
    public override string DisplayName => "现代克制 (Clean SaaS)";
    public override string Description => "基于白底、极简边框和柔和投影的现代SaaS设计风格";
    
    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;
    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.GithubDark;

    public override MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#8b5cf6", // Violet-500
                PrimaryLighten = "#a78bfa",
                PrimaryDarken = "#7c3aed",
                PrimaryContrastText = "#ffffff",

                Secondary = "#64748b", // Slate-500
                SecondaryLighten = "#94a3b8",
                SecondaryDarken = "#475569",
                SecondaryContrastText = "#ffffff",

                Tertiary = "#f1f5f9", // Slate-100 (Useful for pill backgrounds)
                TertiaryContrastText = "#334155", // Slate-700

                Info = "#3b82f6", // Blue-500
                InfoLighten = "#60a5fa",
                InfoDarken = "#2563eb",
                InfoContrastText = "#ffffff",

                Success = "#10b981", // Emerald-500
                SuccessLighten = "#34d399",
                SuccessDarken = "#059669",
                SuccessContrastText = "#ffffff",

                Warning = "#f59e0b", // Amber-500
                WarningLighten = "#fbbf24",
                WarningDarken = "#d97706",
                WarningContrastText = "#ffffff",

                Error = "#ef4444", // Red-500
                ErrorLighten = "#f87171",
                ErrorDarken = "#dc2626",
                ErrorContrastText = "#ffffff",

                Dark = "#0f172a", // Slate-900
                DarkLighten = "#1e293b",
                DarkDarken = "#020617",
                DarkContrastText = "#ffffff",

                // Backgrounds
                Background = "#f8fafc", // Slate-50 (The slightly off-white background)
                BackgroundGray = "#f1f5f9", // Slate-100
                Surface = "#ffffff", // Pure white for cards
                
                // Drawer
                DrawerBackground = "#ffffff",
                DrawerText = "#475569", // Slate-600
                DrawerIcon = "#64748b", // Slate-500

                // Appbar
                AppbarBackground = "rgba(255, 255, 255, 0.85)", // Translucent white for backdrop-blur effect
                AppbarText = "#334155", // Slate-700

                // Text
                TextPrimary = "#0f172a", // Slate-900
                TextSecondary = "#64748b", // Slate-500
                TextDisabled = "#94a3b8", // Slate-400

                // Action
                ActionDefault = "#64748b", // Slate-500
                ActionDisabled = "#cbd5e1", // Slate-300
                ActionDisabledBackground = "#f1f5f9", // Slate-100

                // Borders (Extremely soft)
                Divider = "#e2e8f0", // Slate-200
                DividerLight = "#f1f5f9", // Slate-100
                LinesDefault = "#e2e8f0", // Slate-200
                LinesInputs = "#cbd5e1", // Slate-300

                TableStriped = "#f8fafc", // Slate-50
                TableHover = "#f1f5f9", // Slate-100

                OverlayDark = "rgba(15, 23, 42, 0.5)", // Slate-900 with alpha
                OverlayLight = "rgba(255, 255, 255, 0.7)",

                HoverOpacity = 0.04,

                GrayDefault = "#94a3b8",
                GrayLight = "#cbd5e1",
                GrayLighter = "#f1f5f9",
                GrayDark = "#475569",
                GrayDarker = "#334155"
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#a78bfa", // Violet-400
                PrimaryLighten = "#c4b5fd",
                PrimaryDarken = "#8b5cf6",
                PrimaryContrastText = "#ffffff",

                Secondary = "#94a3b8", // Slate-400
                SecondaryLighten = "#cbd5e1",
                SecondaryDarken = "#64748b",
                SecondaryContrastText = "#0f172a",

                Tertiary = "#1e293b", // Slate-800
                TertiaryContrastText = "#e2e8f0",

                Info = "#60a5fa",
                InfoLighten = "#93c5fd",
                InfoDarken = "#3b82f6",
                InfoContrastText = "#0f172a",

                Success = "#34d399",
                SuccessLighten = "#6ee7b7",
                SuccessDarken = "#10b981",
                SuccessContrastText = "#0f172a",

                Warning = "#fbbf24",
                WarningLighten = "#fcd34d",
                WarningDarken = "#f59e0b",
                WarningContrastText = "#0f172a",

                Error = "#f87171",
                ErrorLighten = "#fca5a5",
                ErrorDarken = "#ef4444",
                ErrorContrastText = "#0f172a",

                Dark = "#f8fafc",
                DarkLighten = "#ffffff",
                DarkDarken = "#e2e8f0",
                DarkContrastText = "#0f172a",

                Background = "#020617", // Slate-950
                BackgroundGray = "#0f172a", // Slate-900
                Surface = "#0f172a", // Slate-900
                
                DrawerBackground = "#0f172a",
                DrawerText = "#cbd5e1",
                DrawerIcon = "#94a3b8",

                AppbarBackground = "rgba(2, 6, 23, 0.85)",
                AppbarText = "#f8fafc",

                TextPrimary = "#f8fafc", // Slate-50
                TextSecondary = "#94a3b8", // Slate-400
                TextDisabled = "#475569", // Slate-600

                ActionDefault = "#94a3b8",
                ActionDisabled = "#334155",
                ActionDisabledBackground = "#1e293b",

                Divider = "#1e293b", // Slate-800
                DividerLight = "#0f172a", // Slate-900
                LinesDefault = "#1e293b", // Slate-800
                LinesInputs = "#334155", // Slate-700

                TableStriped = "#020617",
                TableHover = "#1e293b",

                OverlayDark = "rgba(0, 0, 0, 0.7)",
                OverlayLight = "rgba(15, 23, 42, 0.5)",

                HoverOpacity = 0.06,

                GrayDefault = "#64748b",
                GrayLight = "#94a3b8",
                GrayLighter = "#cbd5e1",
                GrayDark = "#475569",
                GrayDarker = "#334155"
            },
           
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "12px", // Smooth, modern rounding
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "260px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px",
                AppbarHeight = "56px", // Slightly slimmer topbar
            },
            
            // Custom soft diffuse shadows replacing standard Material Elevation
            Shadows = new Shadow()
            {
                Elevation = new[]
                {
                    "none",
                    "0 1px 2px 0 rgba(0, 0, 0, 0.05)", // 1
                    "0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px -1px rgba(0, 0, 0, 0.1)", // 2
                    "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1)", // 3
                    "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -4px rgba(0, 0, 0, 0.1)", // 4
                    "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.1)", // 5
                    "0 25px 50px -12px rgba(0, 0, 0, 0.25)", // 6
                    
                    // The rest map to the softest large shadow to avoid harsh drops
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 7
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 8
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 9
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 10
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 11
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 12
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 13
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 14
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 15
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 16
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 17
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 18
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 19
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 20
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 21
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 22
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 23
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)", // 24
                    "0 4px 20px -2px rgba(0, 0, 0, 0.04)"  // 25
                }
            },
            
            // Custom modern typography mapping
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Inter", "Plus Jakarta Sans", "Helvetica Neue", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0"
                },
                H1 = new H1Typography() { FontSize = "3rem", FontWeight = "700", LineHeight = "1", LetterSpacing = "-0.025em" },
                H2 = new H2Typography() { FontSize = "2.25rem", FontWeight = "700", LineHeight = "2.5rem", LetterSpacing = "-0.025em" },
                H3 = new H3Typography() { FontSize = "1.875rem", FontWeight = "600", LineHeight = "2.25rem", LetterSpacing = "-0.025em" },
                H4 = new H4Typography() { FontSize = "1.5rem", FontWeight = "600", LineHeight = "2rem", LetterSpacing = "-0.025em" },
                H5 = new H5Typography() { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.75rem", LetterSpacing = "-0.025em" },
                H6 = new H6Typography() { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.75rem", LetterSpacing = "-0.025em" },
                Subtitle1 = new Subtitle1Typography() { FontSize = "1rem", FontWeight = "500", LineHeight = "1.5rem", LetterSpacing = "0" },
                Subtitle2 = new Subtitle2Typography() { FontSize = "0.875rem", FontWeight = "500", LineHeight = "1.25rem", LetterSpacing = "0" },
                Body1 = new Body1Typography() { FontSize = "1rem", FontWeight = "400", LineHeight = "1.5rem", LetterSpacing = "0" },
                Body2 = new Body2Typography() { FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.25rem", LetterSpacing = "0" },
                Button = new ButtonTypography() { FontSize = "0.875rem", FontWeight = "500", LineHeight = "1.25rem", LetterSpacing = "0", TextTransform = "none" },
                Caption = new CaptionTypography() { FontSize = "0.75rem", FontWeight = "500", LineHeight = "1rem", LetterSpacing = "0" },
                Overline = new OverlineTypography() { FontSize = "0.75rem", FontWeight = "600", LineHeight = "1rem", LetterSpacing = "0.05em", TextTransform = "uppercase" }
            }
        };
    }
}