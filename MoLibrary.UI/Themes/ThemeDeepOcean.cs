using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 深海静谧主题 - 深海主题
/// </summary>
public class ThemeDeepOcean : IThemeProvider
{
    public string Name => "deep-ocean";
    public string DisplayName => "深海静谧";
    public string Description => "深海主题。深蓝到墨绿的渐变背景，配合水波纹动画效果。使用生物发光元素作为强调色，营造神秘宁静的氛围。适合冥想、睡眠类应用。";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#1e3a8a",           // 深海蓝
                Secondary = "#065f46",         // 深海绿
                Tertiary = "#0e7490",          // 海蓝绿
                Info = "#0891b2",              // 浅海蓝
                Success = "#059669",           // 海藻绿
                Warning = "#d97706",           // 琥珀
                Error = "#dc2626",             // 珊瑚红
                Dark = "#0c4a6e",              // 深海靛蓝
                
                Background = "#f0f9ff",        // 浅海泡沫
                BackgroundGray = "#e0f2fe",    // 浅海蓝
                Surface = "#ffffff",           // 水面白
                AppbarBackground = "#1e3a8a",  // 深海蓝导航
                AppbarText = "#f0f9ff",        // 浅海泡沫文字
                DrawerBackground = "#f0f9ff",  // 浅海泡沫抽屉
                DrawerText = "#0c4a6e",        // 深海靛蓝文字
                DrawerIcon = "#1e3a8a",        // 深海蓝图标
                
                TextPrimary = "#0c4a6e",       // 深海靛蓝文字
                TextSecondary = "#0369a1",     // 海蓝文字
                TextDisabled = "#7dd3fc",      // 浅海蓝文字
                
                ActionDefault = "#1e3a8a",     // 深海蓝
                ActionDisabled = "#bae6fd",    // 极浅海蓝
                ActionDisabledBackground = "#f0f9ff", // 浅海泡沫背景
                
                Divider = "#bae6fd",           // 极浅海蓝分隔线
                DividerLight = "#e0f2fe",      // 浅海蓝分隔线
                
                TableLines = "#bae6fd",        // 表格线
                TableStriped = "#f0f9ff",      // 表格条纹
                TableHover = "#e0f2fe",        // 表格悬停
                
                LinesDefault = "#bae6fd",      // 默认线条
                LinesInputs = "#7dd3fc",       // 输入框线条
                
                GrayDefault = "#0369a1",       // 默认灰色
                GrayLight = "#7dd3fc",         // 浅灰色
                GrayLighter = "#bae6fd",       // 更浅灰色
                GrayDark = "#0284c7",          // 深灰色
                GrayDarker = "#0c4a6e",        // 更深灰色
                
                OverlayDark = "rgba(12,74,110,0.3)",      // 深海遮罩
                OverlayLight = "rgba(240,249,255,0.8)"    // 浅色遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#22d3ee",           // 生物发光青
                Secondary = "#10b981",         // 生物发光绿
                Tertiary = "#06b6d4",          // 青绿发光
                Info = "#3b82f6",              // 深海蓝光
                Success = "#059669",           // 海藻绿光
                Warning = "#f59e0b",           // 深海琥珀
                Error = "#f87171",             // 珊瑚红光
                Dark = "#0f172a",              // 深海黑
                
                Background = "#0f172a",        // 深海黑背景
                BackgroundGray = "#1e293b",    // 深海灰背景
                Surface = "#1e293b",           // 深海灰表面
                AppbarBackground = "#1e293b",  // 深海灰导航
                AppbarText = "#22d3ee",        // 生物发光青文字
                DrawerBackground = "#1e293b",  // 深海灰抽屉
                DrawerText = "#e2e8f0",        // 浅色文字
                DrawerIcon = "#22d3ee",        // 生物发光青图标
                
                TextPrimary = "#f1f5f9",       // 极浅色文字
                TextSecondary = "#cbd5e0",     // 浅色文字
                TextDisabled = "#64748b",      // 中灰文字
                
                ActionDefault = "#22d3ee",     // 生物发光青
                ActionDisabled = "#334155",    // 深灰
                ActionDisabledBackground = "#1e293b", // 深海灰背景
                
                Divider = "#334155",           // 深灰分隔线
                DividerLight = "#475569",      // 中灰分隔线
                
                TableLines = "#334155",        // 表格线
                TableStriped = "#1e293b",      // 表格条纹
                TableHover = "#334155",        // 表格悬停
                
                LinesDefault = "#334155",      // 默认线条
                LinesInputs = "#475569",       // 输入框线条
                
                GrayDefault = "#64748b",       // 默认灰色
                GrayLight = "#94a3b8",         // 浅灰色
                GrayLighter = "#cbd5e0",       // 更浅灰色
                GrayDark = "#475569",          // 深灰色
                GrayDarker = "#334155",        // 更深灰色
                
                OverlayDark = "rgba(15,23,42,0.8)",       // 深海黑遮罩
                OverlayLight = "rgba(30,41,59,0.6)"       // 深海灰遮罩
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "8px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px"
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0.01071em"
                },
                H1 = new H1Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "3.5rem",
                    FontWeight = "300",
                    LineHeight = "1.2",
                    LetterSpacing = "-0.01562em"
                },
                H2 = new H2Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "2.75rem",
                    FontWeight = "300",
                    LineHeight = "1.25",
                    LetterSpacing = "-0.00833em"
                },
                H3 = new H3Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "2.25rem",
                    FontWeight = "400",
                    LineHeight = "1.3",
                    LetterSpacing = "0em"
                },
                H4 = new H4Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "1.75rem",
                    FontWeight = "400",
                    LineHeight = "1.35",
                    LetterSpacing = "0.00735em"
                },
                H5 = new H5Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "1.25rem",
                    FontWeight = "500",
                    LineHeight = "1.4",
                    LetterSpacing = "0em"
                },
                H6 = new H6Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "1.125rem",
                    FontWeight = "500",
                    LineHeight = "1.45",
                    LetterSpacing = "0.0075em"
                },
                Button = new ButtonTypography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.5",
                    LetterSpacing = "0.02857em",
                    TextTransform = "none"
                },
                Body1 = new Body1Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.6",
                    LetterSpacing = "0.00938em"
                },
                Body2 = new Body2Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.55",
                    LetterSpacing = "0.01071em"
                },
                Caption = new CaptionTypography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "1.7",
                    LetterSpacing = "0.03333em"
                },
                Subtitle1 = new Subtitle1Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.65",
                    LetterSpacing = "0.00938em"
                },
                Subtitle2 = new Subtitle2Typography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.6",
                    LetterSpacing = "0.00714em"
                },
                Overline = new OverlineTypography()
                {
                    FontFamily = new[] { "Source Sans Pro", "Open Sans", "Arial", "sans-serif" },
                    FontSize = "0.75rem",
                    FontWeight = "500",
                    LineHeight = "2.2",
                    LetterSpacing = "0.08333em",
                    TextTransform = "uppercase"
                }
            },
            Shadows = new Shadow()
            {
                Elevation = new string[]
                {
                    "none",
                    "0 2px 8px rgba(30, 58, 138, 0.1)",
                    "0 4px 12px rgba(30, 58, 138, 0.15)",
                    "0 6px 16px rgba(30, 58, 138, 0.2)",
                    "0 8px 20px rgba(30, 58, 138, 0.25)",
                    "0 10px 24px rgba(30, 58, 138, 0.3)",
                    "0 12px 28px rgba(30, 58, 138, 0.35)",
                    "0 14px 32px rgba(30, 58, 138, 0.4)",
                    "0 16px 36px rgba(30, 58, 138, 0.45)",
                    "0 18px 40px rgba(30, 58, 138, 0.5)",
                    "0 20px 44px rgba(30, 58, 138, 0.55)",
                    "0 22px 48px rgba(30, 58, 138, 0.6)",
                    "0 24px 52px rgba(30, 58, 138, 0.65)",
                    "0 26px 56px rgba(30, 58, 138, 0.7)",
                    "0 28px 60px rgba(30, 58, 138, 0.75)",
                    "0 30px 64px rgba(30, 58, 138, 0.8)",
                    "0 32px 68px rgba(30, 58, 138, 0.85)",
                    "0 34px 72px rgba(30, 58, 138, 0.9)",
                    "0 36px 76px rgba(30, 58, 138, 0.95)",
                    "0 38px 80px rgba(30, 58, 138, 1.0)",
                    "0 40px 84px rgba(30, 58, 138, 1.0)",
                    "0 42px 88px rgba(30, 58, 138, 1.0)",
                    "0 44px 92px rgba(30, 58, 138, 1.0)",
                    "0 46px 96px rgba(30, 58, 138, 1.0)",
                    "0 48px 100px rgba(30, 58, 138, 1.0)",
                    "0 50px 104px rgba(30, 58, 138, 1.0)"
                }
            },
            ZIndex = new ZIndex()
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
}