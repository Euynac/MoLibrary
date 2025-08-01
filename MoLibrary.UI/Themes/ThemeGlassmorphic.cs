using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 毛玻璃主题 - 现代化透明玻璃效果
/// </summary>
public class ThemeGlassmorphic : ThemeBase
{
    public override string Name => "glassmorphic";
    public override string DisplayName => "毛玻璃主题";
    public override string Description => "现代化透明玻璃效果，支持模糊背景和渐变色彩";
    
    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Github;
    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.MonokaiSublime;

    public override MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                // 主要颜色 - 清新渐变色
                Primary = "#667eea",
                PrimaryLighten = "#a8b8ff",
                PrimaryDarken = "#3d4ed8",
                PrimaryContrastText = "#ffffff",
                
                // 次要颜色 - 温暖渐变色
                Secondary = "#f093fb",
                SecondaryLighten = "#ffc3ff",
                SecondaryDarken = "#b665c7",
                SecondaryContrastText = "#ffffff",
                
                // 第三色调 - 清新蓝色
                Tertiary = "#22d3ee",
                TertiaryLighten = "#67e8f9",
                TertiaryDarken = "#0891b2",
                TertiaryContrastText = "#ffffff",
                
                // 状态颜色
                Info = "#3b82f6",
                InfoLighten = "#93c5fd",
                InfoDarken = "#1d4ed8",
                InfoContrastText = "#ffffff",
                
                Success = "#10b981",
                SuccessLighten = "#6ee7b7",
                SuccessDarken = "#047857",
                SuccessContrastText = "#ffffff",
                
                Warning = "#f59e0b",
                WarningLighten = "#fbbf24",
                WarningDarken = "#d97706",
                WarningContrastText = "#ffffff",
                
                Error = "#ef4444",
                ErrorLighten = "#f87171",
                ErrorDarken = "#dc2626",
                ErrorContrastText = "#ffffff",
                
                Dark = "#374151",
                DarkLighten = "#6b7280",
                DarkDarken = "#1f2937",
                DarkContrastText = "#ffffff",
                
                // 文本颜色 - 适配透明背景
                TextPrimary = "rgba(31, 41, 55, 0.9)",
                TextSecondary = "rgba(75, 85, 99, 0.8)",
                TextDisabled = "rgba(156, 163, 175, 0.6)",
                
                // 操作颜色
                ActionDefault = "rgba(31, 41, 55, 0.9)",
                ActionDisabled = "rgba(156, 163, 175, 0.5)",
                ActionDisabledBackground = "rgba(243, 244, 246, 0.3)",
                
                // 背景色 - 透明渐变
                Background = "rgba(255, 255, 255, 0.85)",
                BackgroundGray = "rgba(249, 250, 251, 0.8)",
                Surface = "rgba(255, 255, 255, 0.75)",
                
                // 应用栏和抽屉
                DrawerBackground = "rgba(255, 255, 255, 0.85)",
                DrawerText = "rgba(31, 41, 55, 0.9)",
                DrawerIcon = "rgba(75, 85, 99, 0.7)",
                AppbarBackground = "rgba(255, 255, 255, 0.8)",
                AppbarText = "rgba(31, 41, 55, 0.9)",
                
                // 线条和边框
                LinesDefault = "rgba(229, 231, 235, 0.6)",
                LinesInputs = "rgba(156, 163, 175, 0.7)",
                TableLines = "rgba(229, 231, 235, 0.5)",
                TableStriped = "rgba(249, 250, 251, 0.4)",
                TableHover = "rgba(243, 244, 246, 0.6)",
                Divider = "rgba(229, 231, 235, 0.5)",
                DividerLight = "rgba(243, 244, 246, 0.3)",
                
                HoverOpacity = 0.08,
                
                // 灰度颜色
                GrayDefault = "#9ca3af",
                GrayLight = "#e5e7eb",
                GrayLighter = "#f9fafb",
                GrayDark = "#6b7280",
                GrayDarker = "#374151",
                
                // 遮罩
                OverlayDark = "rgba(31, 41, 55, 0.4)",
                OverlayLight = "rgba(255, 255, 255, 0.3)"
            },
            PaletteDark = new PaletteDark()
            {
                // 主要颜色 - 明亮霓虹色
                Primary = "#00d4ff",
                PrimaryLighten = "#66e0ff",
                PrimaryDarken = "#0099cc",
                PrimaryContrastText = "#0f172a",
                
                // 次要颜色 - 粉紫霓虹
                Secondary = "#ff006e",
                SecondaryLighten = "#ff5aa8",
                SecondaryDarken = "#c70055",
                SecondaryContrastText = "#ffffff",
                
                // 第三色调 - 翠绿霓虹
                Tertiary = "#39ff14",
                TertiaryLighten = "#7fff66",
                TertiaryDarken = "#2dd40a",
                TertiaryContrastText = "#0f172a",
                
                // 状态颜色 - 霓虹亮色
                Info = "#0ea5e9",
                InfoLighten = "#38bdf8",
                InfoDarken = "#0284c7",
                InfoContrastText = "#ffffff",
                
                Success = "#22c55e",
                SuccessLighten = "#4ade80",
                SuccessDarken = "#16a34a",
                SuccessContrastText = "#0f172a",
                
                Warning = "#eab308",
                WarningLighten = "#facc15",
                WarningDarken = "#ca8a04",
                WarningContrastText = "#0f172a",
                
                Error = "#e11d48",
                ErrorLighten = "#f43f5e",
                ErrorDarken = "#be123c",
                ErrorContrastText = "#ffffff",
                
                Dark = "#1e293b",
                DarkLighten = "#334155",
                DarkDarken = "#0f172a",
                DarkContrastText = "#f8fafc",
                
                // 文本颜色 - 高对比度
                TextPrimary = "rgba(248, 250, 252, 0.95)",
                TextSecondary = "rgba(203, 213, 225, 0.8)",
                TextDisabled = "rgba(148, 163, 184, 0.5)",
                
                // 操作颜色
                ActionDefault = "rgba(248, 250, 252, 0.9)",
                ActionDisabled = "rgba(148, 163, 184, 0.4)",
                ActionDisabledBackground = "rgba(30, 41, 59, 0.4)",
                
                // 背景色 - 深色透明
                Background = "rgba(15, 23, 42, 0.9)",
                BackgroundGray = "rgba(30, 41, 59, 0.85)",
                Surface = "rgba(30, 41, 59, 0.8)",
                
                // 应用栏和抽屉
                DrawerBackground = "rgba(30, 41, 59, 0.9)",
                DrawerText = "rgba(248, 250, 252, 0.9)",
                DrawerIcon = "rgba(203, 213, 225, 0.7)",
                AppbarBackground = "rgba(30, 41, 59, 0.85)",
                AppbarText = "rgba(248, 250, 252, 0.9)",
                
                // 线条和边框 - 霓虹边框
                LinesDefault = "rgba(51, 65, 85, 0.6)",
                LinesInputs = "rgba(100, 116, 139, 0.8)",
                TableLines = "rgba(51, 65, 85, 0.5)",
                TableStriped = "rgba(30, 41, 59, 0.3)",
                TableHover = "rgba(51, 65, 85, 0.4)",
                Divider = "rgba(51, 65, 85, 0.5)",
                DividerLight = "rgba(30, 41, 59, 0.3)",
                
                HoverOpacity = 0.12,
                
                // 灰度颜色
                GrayDefault = "#64748b",
                GrayLight = "#94a3b8",
                GrayLighter = "#cbd5e1",
                GrayDark = "#475569",
                GrayDarker = "#334155",
                
                // 遮罩
                OverlayDark = "rgba(15, 23, 42, 0.6)",
                OverlayLight = "rgba(248, 250, 252, 0.1)"
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "16px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px",
                AppbarHeight = "72px"
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Inter", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0.02em"
                },
                H1 = new H1Typography()
                {
                    FontSize = "3.5rem",
                    FontWeight = "700",
                    LineHeight = "1.2",
                    LetterSpacing = "-0.025em"
                },
                H2 = new H2Typography()
                {
                    FontSize = "2.5rem",
                    FontWeight = "600",
                    LineHeight = "1.3",
                    LetterSpacing = "-0.02em"
                },
                H3 = new H3Typography()
                {
                    FontSize = "2rem",
                    FontWeight = "600",
                    LineHeight = "1.35",
                    LetterSpacing = "-0.01em"
                },
                H4 = new H4Typography()
                {
                    FontSize = "1.5rem",
                    FontWeight = "600",
                    LineHeight = "1.4",
                    LetterSpacing = "0em"
                },
                H5 = new H5Typography()
                {
                    FontSize = "1.25rem",
                    FontWeight = "600",
                    LineHeight = "1.5",
                    LetterSpacing = "0em"
                },
                H6 = new H6Typography()
                {
                    FontSize = "1.125rem",
                    FontWeight = "600",
                    LineHeight = "1.6",
                    LetterSpacing = "0em"
                },
                Button = new ButtonTypography()
                {
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.75",
                    LetterSpacing = "0.02em",
                    TextTransform = "none"
                }
            },
            Shadows = new Shadow(),
            ZIndex = new ZIndex()
        };
    }
}