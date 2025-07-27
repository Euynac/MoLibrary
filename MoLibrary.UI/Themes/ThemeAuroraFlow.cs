using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 极光流彩主题 - 现代渐变主题，灵感来自北极光
/// </summary>
public class ThemeAuroraFlow : IThemeProvider
{
    public string Name => "aurora-flow";
    public string DisplayName => "极光流彩";
    public string Description => "现代渐变主题，灵感来自北极光。使用流动的渐变色彩，主色调为深紫到青绿的过渡，配合玻璃态效果和微妙的光晕动画。适合科技产品和创意应用。";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#667eea",           // 深紫蓝
                Secondary = "#00d4ff",         // 极光青
                Tertiary = "#f093fb",          // 极光粉
                Info = "#00bcd4",              // 极光蓝绿
                Success = "#4caf50",           // 极光绿
                Warning = "#ff9800",           // 极光橙
                Error = "#f44336",             // 极光红
                Dark = "#2d3748",              // 深蓝灰
                
                Background = "#fafbfc",        // 极浅蓝白
                BackgroundGray = "#f7fafc",    // 浅蓝白
                Surface = "#ffffff",           // 纯白
                AppbarBackground = "#667eea",  // 与主色相同
                AppbarText = "#ffffff",        // 白色文字
                DrawerBackground = "#ffffff",  // 白色抽屉
                DrawerText = "#2d3748",        // 深色文字
                DrawerIcon = "#667eea",        // 主色图标
                
                TextPrimary = "#1a202c",       // 深蓝黑
                TextSecondary = "#4a5568",     // 中蓝灰
                TextDisabled = "#a0aec0",      // 浅蓝灰
                
                ActionDefault = "#667eea",     // 主色
                ActionDisabled = "#e2e8f0",    // 极浅灰
                ActionDisabledBackground = "#f7fafc", // 浅背景
                
                Divider = "#e2e8f0",           // 极浅灰分隔线
                DividerLight = "#f1f5f9",      // 更浅分隔线
                
                TableLines = "#e2e8f0",        // 表格线
                TableStriped = "#f8fafc",      // 表格条纹
                TableHover = "#edf2f7",        // 表格悬停
                
                LinesDefault = "#e2e8f0",      // 默认线条
                LinesInputs = "#cbd5e0",       // 输入框线条
                
                GrayDefault = "#718096",       // 默认灰色
                GrayLight = "#a0aec0",         // 浅灰色
                GrayLighter = "#e2e8f0",       // 更浅灰色
                GrayDark = "#4a5568",          // 深灰色
                GrayDarker = "#2d3748",        // 更深灰色
                
                OverlayDark = "rgba(33,37,41,0.3)",    // 深色遮罩
                OverlayLight = "rgba(255,255,255,0.5)"  // 浅色遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#00d4ff",           // 极光青
                Secondary = "#ff006e",         // 极光粉红
                Tertiary = "#8b5cf6",          // 极光紫
                Info = "#06b6d4",              // 青蓝
                Success = "#10b981",           // 翠绿
                Warning = "#f59e0b",           // 琥珀
                Error = "#ef4444",             // 红色
                Dark = "#0f172a",              // 深夜蓝
                
                Background = "#0f172a",        // 深夜蓝背景
                BackgroundGray = "#1e293b",    // 深蓝灰背景
                Surface = "#1e293b",           // 深蓝灰表面
                AppbarBackground = "#1e293b",  // 深蓝灰导航栏
                AppbarText = "#f1f5f9",        // 浅色文字
                DrawerBackground = "#1e293b",  // 深蓝灰抽屉
                DrawerText = "#f1f5f9",        // 浅色文字
                DrawerIcon = "#00d4ff",        // 极光青图标
                
                TextPrimary = "#f8fafc",       // 极浅色文字
                TextSecondary = "#cbd5e0",     // 浅蓝灰文字
                TextDisabled = "#64748b",      // 中蓝灰文字
                
                ActionDefault = "#00d4ff",     // 极光青
                ActionDisabled = "#334155",    // 深蓝灰
                ActionDisabledBackground = "#1e293b", // 深背景
                
                Divider = "#334155",           // 深蓝灰分隔线
                DividerLight = "#475569",      // 中蓝灰分隔线
                
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
                
                OverlayDark = "rgba(15,23,42,0.8)",    // 深色遮罩
                OverlayLight = "rgba(30,41,59,0.6)"    // 中色遮罩
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "12px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px"
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.43",
                    LetterSpacing = "0.01071em"
                },
                H1 = new H1Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "6rem",
                    FontWeight = "300",
                    LineHeight = "1.167",
                    LetterSpacing = "-0.01562em"
                },
                H2 = new H2Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "3.75rem",
                    FontWeight = "300",
                    LineHeight = "1.2",
                    LetterSpacing = "-0.00833em"
                },
                H3 = new H3Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "3rem",
                    FontWeight = "400",
                    LineHeight = "1.167",
                    LetterSpacing = "0em"
                },
                H4 = new H4Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "2.125rem",
                    FontWeight = "400",
                    LineHeight = "1.235",
                    LetterSpacing = "0.00735em"
                },
                H5 = new H5Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "1.5rem",
                    FontWeight = "400",
                    LineHeight = "1.334",
                    LetterSpacing = "0em"
                },
                H6 = new H6Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "1.25rem",
                    FontWeight = "500",
                    LineHeight = "1.6",
                    LetterSpacing = "0.0075em"
                },
                Button = new ButtonTypography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.75",
                    LetterSpacing = "0.02857em",
                    TextTransform = "none"
                },
                Body1 = new Body1Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0.00938em"
                },
                Body2 = new Body2Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.43",
                    LetterSpacing = "0.01071em"
                },
                Caption = new CaptionTypography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "1.66",
                    LetterSpacing = "0.03333em"
                },
                Subtitle1 = new Subtitle1Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.75",
                    LetterSpacing = "0.00938em"
                },
                Subtitle2 = new Subtitle2Typography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.57",
                    LetterSpacing = "0.00714em"
                },
                Overline = new OverlineTypography()
                {
                    FontFamily = new[] { "Inter", "Roboto", "Arial", "sans-serif" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "2.66",
                    LetterSpacing = "0.08333em",
                    TextTransform = "uppercase"
                }
            },
            Shadows = new Shadow()
            {
                Elevation = new string[]
                {
                    "none",
                    "0 2px 4px rgba(102, 126, 234, 0.1)",
                    "0 4px 8px rgba(102, 126, 234, 0.15)",
                    "0 6px 12px rgba(102, 126, 234, 0.2)",
                    "0 8px 16px rgba(102, 126, 234, 0.25)",
                    "0 10px 20px rgba(102, 126, 234, 0.3)",
                    "0 12px 24px rgba(102, 126, 234, 0.35)",
                    "0 14px 28px rgba(102, 126, 234, 0.4)",
                    "0 16px 32px rgba(102, 126, 234, 0.45)",
                    "0 18px 36px rgba(102, 126, 234, 0.5)",
                    "0 20px 40px rgba(102, 126, 234, 0.55)",
                    "0 22px 44px rgba(102, 126, 234, 0.6)",
                    "0 24px 48px rgba(102, 126, 234, 0.65)",
                    "0 26px 52px rgba(102, 126, 234, 0.7)",
                    "0 28px 56px rgba(102, 126, 234, 0.75)",
                    "0 30px 60px rgba(102, 126, 234, 0.8)",
                    "0 32px 64px rgba(102, 126, 234, 0.85)",
                    "0 34px 68px rgba(102, 126, 234, 0.9)",
                    "0 36px 72px rgba(102, 126, 234, 0.95)",
                    "0 38px 76px rgba(102, 126, 234, 1.0)",
                    "0 40px 80px rgba(102, 126, 234, 1.0)",
                    "0 42px 84px rgba(102, 126, 234, 1.0)",
                    "0 44px 88px rgba(102, 126, 234, 1.0)",
                    "0 46px 92px rgba(102, 126, 234, 1.0)",
                    "0 48px 96px rgba(102, 126, 234, 1.0)",
                    "0 50px 100px rgba(102, 126, 234, 1.0)"
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