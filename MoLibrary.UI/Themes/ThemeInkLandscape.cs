using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 墨韵山水主题 - 中国水墨画风格主题
/// </summary>
public class ThemeInkLandscape : IThemeProvider
{
    public string Name => "ink-landscape";
    public string DisplayName => "墨韵山水";
    public string Description => "中国水墨画风格主题。以黑白灰为主调，点缀淡雅的青绿或赭石色。使用留白设计，配合毛笔笔触效果的分割线和按钮。适合文化类、阅读类应用。";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#2d3748",           // 墨黑
                Secondary = "#4a5568",         // 深灰墨
                Tertiary = "#718096",          // 中灰墨
                Info = "#2c7a7b",              // 青绿
                Success = "#38a169",           // 翠绿
                Warning = "#d69e2e",           // 赭石
                Error = "#e53e3e",             // 朱红
                Dark = "#1a202c",              // 浓墨
                
                Background = "#fefefe",        // 宣纸白
                BackgroundGray = "#f9f9f9",    // 淡灰白
                Surface = "#ffffff",           // 纯白
                AppbarBackground = "#2d3748",  // 墨黑导航
                AppbarText = "#f7fafc",        // 淡墨文字
                DrawerBackground = "#fefefe",  // 宣纸白抽屉
                DrawerText = "#2d3748",        // 墨黑文字
                DrawerIcon = "#4a5568",        // 深灰墨图标
                
                TextPrimary = "#1a202c",       // 浓墨文字
                TextSecondary = "#4a5568",     // 深灰墨文字
                TextDisabled = "#a0aec0",      // 浅灰墨文字
                
                ActionDefault = "#2d3748",     // 墨黑
                ActionDisabled = "#e2e8f0",    // 极浅灰
                ActionDisabledBackground = "#f7fafc", // 淡背景
                
                Divider = "#e2e8f0",           // 淡墨线
                DividerLight = "#f1f5f9",      // 极淡墨线
                
                TableLines = "#e2e8f0",        // 表格墨线
                TableStriped = "#f8fafc",      // 表格条纹
                TableHover = "#edf2f7",        // 表格悬停
                
                LinesDefault = "#e2e8f0",      // 默认墨线
                LinesInputs = "#cbd5e0",       // 输入框墨线
                
                GrayDefault = "#718096",       // 默认灰墨
                GrayLight = "#a0aec0",         // 浅灰墨
                GrayLighter = "#e2e8f0",       // 更浅灰墨
                GrayDark = "#4a5568",          // 深灰墨
                GrayDarker = "#2d3748",        // 更深灰墨
                
                OverlayDark = "rgba(45,55,72,0.3)",    // 深墨遮罩
                OverlayLight = "rgba(255,255,255,0.8)"  // 浅色遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#e2e8f0",           // 淡墨
                Secondary = "#cbd5e0",         // 浅灰墨
                Tertiary = "#a0aec0",          // 中浅灰墨
                Info = "#4fd1c7",              // 青绿
                Success = "#68d391",           // 翠绿
                Warning = "#fbd38d",           // 赭石
                Error = "#fc8181",             // 朱红
                Dark = "#f7fafc",              // 淡色
                
                Background = "#1a202c",        // 浓墨背景
                BackgroundGray = "#2d3748",    // 深墨背景
                Surface = "#2d3748",           // 深墨表面
                AppbarBackground = "#2d3748",  // 深墨导航
                AppbarText = "#e2e8f0",        // 淡墨文字
                DrawerBackground = "#2d3748",  // 深墨抽屉
                DrawerText = "#e2e8f0",        // 淡墨文字
                DrawerIcon = "#cbd5e0",        // 浅灰墨图标
                
                TextPrimary = "#f7fafc",       // 淡色文字
                TextSecondary = "#e2e8f0",     // 淡墨文字
                TextDisabled = "#718096",      // 中灰墨文字
                
                ActionDefault = "#e2e8f0",     // 淡墨
                ActionDisabled = "#4a5568",    // 深灰墨
                ActionDisabledBackground = "#2d3748", // 深背景
                
                Divider = "#4a5568",           // 深灰墨分隔线
                DividerLight = "#718096",      // 中灰墨分隔线
                
                TableLines = "#4a5568",        // 表格墨线
                TableStriped = "#2d3748",      // 表格条纹
                TableHover = "#4a5568",        // 表格悬停
                
                LinesDefault = "#4a5568",      // 默认墨线
                LinesInputs = "#718096",       // 输入框墨线
                
                GrayDefault = "#718096",       // 默认灰墨
                GrayLight = "#a0aec0",         // 浅灰墨
                GrayLighter = "#cbd5e0",       // 更浅灰墨
                GrayDark = "#4a5568",          // 深灰墨
                GrayDarker = "#2d3748",        // 更深灰墨
                
                OverlayDark = "rgba(26,32,44,0.8)",    // 浓墨遮罩
                OverlayLight = "rgba(45,55,72,0.6)"    // 深墨遮罩
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "2px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px"
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.6",
                    LetterSpacing = "0.01071em"
                },
                H1 = new H1Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "3rem",
                    FontWeight = "300",
                    LineHeight = "1.3",
                    LetterSpacing = "-0.01562em"
                },
                H2 = new H2Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "2.5rem",
                    FontWeight = "300",
                    LineHeight = "1.35",
                    LetterSpacing = "-0.00833em"
                },
                H3 = new H3Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "2rem",
                    FontWeight = "400",
                    LineHeight = "1.4",
                    LetterSpacing = "0em"
                },
                H4 = new H4Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "1.5rem",
                    FontWeight = "400",
                    LineHeight = "1.45",
                    LetterSpacing = "0.00735em"
                },
                H5 = new H5Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "1.25rem",
                    FontWeight = "400",
                    LineHeight = "1.5",
                    LetterSpacing = "0em"
                },
                H6 = new H6Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "1.125rem",
                    FontWeight = "500",
                    LineHeight = "1.55",
                    LetterSpacing = "0.0075em"
                },
                Button = new ButtonTypography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.6",
                    LetterSpacing = "0.02857em",
                    TextTransform = "none"
                },
                Body1 = new Body1Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.7",
                    LetterSpacing = "0.00938em"
                },
                Body2 = new Body2Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.65",
                    LetterSpacing = "0.01071em"
                },
                Caption = new CaptionTypography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "1.8",
                    LetterSpacing = "0.03333em"
                },
                Subtitle1 = new Subtitle1Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.75",
                    LetterSpacing = "0.00938em"
                },
                Subtitle2 = new Subtitle2Typography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.6",
                    LetterSpacing = "0.00714em"
                },
                Overline = new OverlineTypography()
                {
                    FontFamily = new[] { "Noto Serif SC", "Source Han Serif SC", "serif" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "2.5",
                    LetterSpacing = "0.08333em",
                    TextTransform = "none"
                }
            },
            Shadows = new Shadow()
            {
                Elevation = new string[]
                {
                    "none",
                    "0 1px 3px rgba(45, 55, 72, 0.1)",
                    "0 2px 6px rgba(45, 55, 72, 0.15)",
                    "0 3px 9px rgba(45, 55, 72, 0.2)",
                    "0 4px 12px rgba(45, 55, 72, 0.25)",
                    "0 5px 15px rgba(45, 55, 72, 0.3)",
                    "0 6px 18px rgba(45, 55, 72, 0.35)",
                    "0 7px 21px rgba(45, 55, 72, 0.4)",
                    "0 8px 24px rgba(45, 55, 72, 0.45)",
                    "0 9px 27px rgba(45, 55, 72, 0.5)",
                    "0 10px 30px rgba(45, 55, 72, 0.55)",
                    "0 11px 33px rgba(45, 55, 72, 0.6)",
                    "0 12px 36px rgba(45, 55, 72, 0.65)",
                    "0 13px 39px rgba(45, 55, 72, 0.7)",
                    "0 14px 42px rgba(45, 55, 72, 0.75)",
                    "0 15px 45px rgba(45, 55, 72, 0.8)",
                    "0 16px 48px rgba(45, 55, 72, 0.85)",
                    "0 17px 51px rgba(45, 55, 72, 0.9)",
                    "0 18px 54px rgba(45, 55, 72, 0.95)",
                    "0 19px 57px rgba(45, 55, 72, 1.0)",
                    "0 20px 60px rgba(45, 55, 72, 1.0)",
                    "0 21px 63px rgba(45, 55, 72, 1.0)",
                    "0 22px 66px rgba(45, 55, 72, 1.0)",
                    "0 23px 69px rgba(45, 55, 72, 1.0)",
                    "0 24px 72px rgba(45, 55, 72, 1.0)",
                    "0 25px 75px rgba(45, 55, 72, 1.0)"
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