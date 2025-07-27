using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 马卡龙甜心主题 - 柔和粉彩主题
/// </summary>
public class ThemeMacaronSweet : IThemeProvider
{
    public string Name => "macaron-sweet";
    public string DisplayName => "马卡龙甜心";
    public string Description => "柔和粉彩主题。使用马卡龙色系（薄荷绿、樱花粉、奶油黄、薰衣草紫），圆润的设计语言，配合可爱的弹跳动画。适合女性向和生活类应用。";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#ff9aa2",           // 樱花粉
                Secondary = "#b5ead7",         // 薄荷绿
                Tertiary = "#c7ceea",          // 薰衣草紫
                Info = "#a8e6cf",              // 淡绿
                Success = "#88d8c0",           // 柔和绿
                Warning = "#ffd3a5",           // 奶油橙
                Error = "#ffb3ba",             // 柔和粉红
                Dark = "#6b5b95",              // 深紫灰
                
                Background = "#fefcfb",        // 奶油白
                BackgroundGray = "#fbf8f6",    // 淡奶油
                Surface = "#ffffff",           // 纯白
                AppbarBackground = "#ff9aa2",  // 樱花粉导航
                AppbarText = "#ffffff",        // 白色文字
                DrawerBackground = "#fefcfb",  // 奶油白抽屉
                DrawerText = "#6b5b95",        // 深紫灰文字
                DrawerIcon = "#ff9aa2",        // 樱花粉图标
                
                TextPrimary = "#5d4e75",       // 深紫文字
                TextSecondary = "#8e7cc3",     // 中紫文字
                TextDisabled = "#c7ceea",      // 淡紫文字
                
                ActionDefault = "#ff9aa2",     // 樱花粉
                ActionDisabled = "#f0e6ff",    // 极淡紫
                ActionDisabledBackground = "#faf7ff", // 淡紫背景
                
                Divider = "#f0e6ff",           // 极淡紫分隔线
                DividerLight = "#f8f4ff",      // 更淡紫分隔线
                
                TableLines = "#f0e6ff",        // 表格线
                TableStriped = "#faf7ff",      // 表格条纹
                TableHover = "#f5f0ff",        // 表格悬停
                
                LinesDefault = "#f0e6ff",      // 默认线条
                LinesInputs = "#e6d7ff",       // 输入框线条
                
                GrayDefault = "#8e7cc3",       // 默认灰色
                GrayLight = "#c7ceea",         // 浅灰色
                GrayLighter = "#e6d7ff",       // 更浅灰色
                GrayDark = "#6b5b95",          // 深灰色
                GrayDarker = "#5d4e75",        // 更深灰色
                
                OverlayDark = "rgba(107,91,149,0.3)",     // 深紫遮罩
                OverlayLight = "rgba(255,255,255,0.8)"    // 浅色遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#ff8a95",           // 深樱花粉
                Secondary = "#7fcdcd",         // 深薄荷绿
                Tertiary = "#9896f1",          // 深薰衣草紫
                Info = "#7bccc4",              // 深绿松石
                Success = "#6bb6ae",           // 深柔和绿
                Warning = "#ffbe76",           // 深奶油橙
                Error = "#ff8a9b",             // 深柔和粉红
                Dark = "#2d1b69",              // 深紫
                
                Background = "#2d1b69",        // 深紫背景
                BackgroundGray = "#4a2c7a",    // 中紫背景
                Surface = "#4a2c7a",           // 中紫表面
                AppbarBackground = "#4a2c7a",  // 中紫导航
                AppbarText = "#fef7ff",        // 淡色文字
                DrawerBackground = "#4a2c7a",  // 中紫抽屉
                DrawerText = "#fef7ff",        // 淡色文字
                DrawerIcon = "#ff8a95",        // 深樱花粉图标
                
                TextPrimary = "#fef7ff",       // 淡色文字
                TextSecondary = "#e6d7ff",     // 淡紫文字
                TextDisabled = "#8e7cc3",      // 中紫文字
                
                ActionDefault = "#ff8a95",     // 深樱花粉
                ActionDisabled = "#5d4e75",    // 深紫灰
                ActionDisabledBackground = "#4a2c7a", // 中紫背景
                
                Divider = "#5d4e75",           // 深紫灰分隔线
                DividerLight = "#6b5b95",      // 中紫分隔线
                
                TableLines = "#5d4e75",        // 表格线
                TableStriped = "#4a2c7a",      // 表格条纹
                TableHover = "#5d4e75",        // 表格悬停
                
                LinesDefault = "#5d4e75",      // 默认线条
                LinesInputs = "#6b5b95",       // 输入框线条
                
                GrayDefault = "#8e7cc3",       // 默认灰色
                GrayLight = "#c7ceea",         // 浅灰色
                GrayLighter = "#e6d7ff",       // 更浅灰色
                GrayDark = "#6b5b95",          // 深灰色
                GrayDarker = "#5d4e75",        // 更深灰色
                
                OverlayDark = "rgba(45,27,105,0.8)",      // 深紫遮罩
                OverlayLight = "rgba(74,44,122,0.6)"      // 中紫遮罩
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "20px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px"
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0.01071em"
                },
                H1 = new H1Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "3.5rem",
                    FontWeight = "300",
                    LineHeight = "1.2",
                    LetterSpacing = "-0.01562em"
                },
                H2 = new H2Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "2.75rem",
                    FontWeight = "300",
                    LineHeight = "1.25",
                    LetterSpacing = "-0.00833em"
                },
                H3 = new H3Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "2.25rem",
                    FontWeight = "400",
                    LineHeight = "1.3",
                    LetterSpacing = "0em"
                },
                H4 = new H4Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "1.75rem",
                    FontWeight = "400",
                    LineHeight = "1.35",
                    LetterSpacing = "0.00735em"
                },
                H5 = new H5Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "1.25rem",
                    FontWeight = "500",
                    LineHeight = "1.4",
                    LetterSpacing = "0em"
                },
                H6 = new H6Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "1.125rem",
                    FontWeight = "500",
                    LineHeight = "1.45",
                    LetterSpacing = "0.0075em"
                },
                Button = new ButtonTypography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.5",
                    LetterSpacing = "0.02857em",
                    TextTransform = "none"
                },
                Body1 = new Body1Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.6",
                    LetterSpacing = "0.00938em"
                },
                Body2 = new Body2Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.55",
                    LetterSpacing = "0.01071em"
                },
                Caption = new CaptionTypography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "1.7",
                    LetterSpacing = "0.03333em"
                },
                Subtitle1 = new Subtitle1Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.65",
                    LetterSpacing = "0.00938em"
                },
                Subtitle2 = new Subtitle2Typography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.6",
                    LetterSpacing = "0.00714em"
                },
                Overline = new OverlineTypography()
                {
                    FontFamily = new[] { "Comfortaa", "Nunito", "Arial", "sans-serif" },
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
                    "0 2px 8px rgba(255, 154, 162, 0.15)",
                    "0 4px 12px rgba(255, 154, 162, 0.2)",
                    "0 6px 16px rgba(255, 154, 162, 0.25)",
                    "0 8px 20px rgba(255, 154, 162, 0.3)",
                    "0 10px 24px rgba(255, 154, 162, 0.35)",
                    "0 12px 28px rgba(255, 154, 162, 0.4)",
                    "0 14px 32px rgba(255, 154, 162, 0.45)",
                    "0 16px 36px rgba(255, 154, 162, 0.5)",
                    "0 18px 40px rgba(255, 154, 162, 0.55)",
                    "0 20px 44px rgba(255, 154, 162, 0.6)",
                    "0 22px 48px rgba(255, 154, 162, 0.65)",
                    "0 24px 52px rgba(255, 154, 162, 0.7)",
                    "0 26px 56px rgba(255, 154, 162, 0.75)",
                    "0 28px 60px rgba(255, 154, 162, 0.8)",
                    "0 30px 64px rgba(255, 154, 162, 0.85)",
                    "0 32px 68px rgba(255, 154, 162, 0.9)",
                    "0 34px 72px rgba(255, 154, 162, 0.95)",
                    "0 36px 76px rgba(255, 154, 162, 1.0)",
                    "0 38px 80px rgba(255, 154, 162, 1.0)",
                    "0 40px 84px rgba(255, 154, 162, 1.0)",
                    "0 42px 88px rgba(255, 154, 162, 1.0)",
                    "0 44px 92px rgba(255, 154, 162, 1.0)",
                    "0 46px 96px rgba(255, 154, 162, 1.0)",
                    "0 48px 100px rgba(255, 154, 162, 1.0)",
                    "0 50px 104px rgba(255, 154, 162, 1.0)"
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