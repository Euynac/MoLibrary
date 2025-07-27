using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 复古印刷主题 - 怀旧报纸风格
/// </summary>
public class ThemeVintagePress : IThemeProvider
{
    public string Name => "vintage-press";
    public string DisplayName => "复古印刷";
    public string Description => "怀旧报纸风格。米黄色背景，深褐色文字，使用打字机字体和网格布局。配合做旧纹理和墨水污渍效果。适合阅读、写作类应用。";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#8b4513",           // 深褐色
                Secondary = "#654321",         // 咖啡色
                Tertiary = "#daa520",          // 古董金
                Info = "#708090",              // 石板灰
                Success = "#556b2f",           // 橄榄绿
                Warning = "#cd853f",           // 秘鲁褐
                Error = "#a0522d",             // 鞍褐色
                Dark = "#2f1b14",              // 深咖啡
                
                Background = "#faf5e6",        // 米黄色
                BackgroundGray = "#f5f0e1",    // 浅米黄
                Surface = "#fffef7",           // 纸白色
                AppbarBackground = "#8b4513",  // 深褐色导航
                AppbarText = "#faf5e6",        // 米黄色文字
                DrawerBackground = "#faf5e6",  // 米黄色抽屉
                DrawerText = "#2f1b14",        // 深咖啡文字
                DrawerIcon = "#654321",        // 咖啡色图标
                
                TextPrimary = "#2f1b14",       // 深咖啡文字
                TextSecondary = "#654321",     // 咖啡色文字
                TextDisabled = "#a0522d",      // 鞍褐色文字
                
                ActionDefault = "#8b4513",     // 深褐色
                ActionDisabled = "#ddd5c7",    // 浅褐灰
                ActionDisabledBackground = "#f5f0e1", // 浅米黄背景
                
                Divider = "#ddd5c7",           // 浅褐灰分隔线
                DividerLight = "#eae5d8",      // 极浅褐分隔线
                
                TableLines = "#ddd5c7",        // 表格线
                TableStriped = "#f5f0e1",      // 表格条纹
                TableHover = "#f0ead9",        // 表格悬停
                
                LinesDefault = "#ddd5c7",      // 默认线条
                LinesInputs = "#c8b99c",       // 输入框线条
                
                GrayDefault = "#a0522d",       // 默认灰色
                GrayLight = "#ddd5c7",         // 浅灰色
                GrayLighter = "#eae5d8",       // 更浅灰色
                GrayDark = "#654321",          // 深灰色
                GrayDarker = "#2f1b14",        // 更深灰色
                
                OverlayDark = "rgba(47,27,20,0.3)",       // 深咖啡遮罩
                OverlayLight = "rgba(250,245,230,0.8)"    // 米黄遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#daa520",           // 古董金
                Secondary = "#cd853f",         // 秘鲁褐
                Tertiary = "#f4a460",          // 沙褐色
                Info = "#778899",              // 淡石板灰
                Success = "#9acd32",           // 黄绿色
                Warning = "#daa520",           // 古董金
                Error = "#cd5c5c",             // 印第安红
                Dark = "#1a1008",              // 深黑褐
                
                Background = "#1a1008",        // 深黑褐背景
                BackgroundGray = "#2f1b14",    // 深咖啡背景
                Surface = "#2f1b14",           // 深咖啡表面
                AppbarBackground = "#2f1b14",  // 深咖啡导航
                AppbarText = "#daa520",        // 古董金文字
                DrawerBackground = "#2f1b14",  // 深咖啡抽屉
                DrawerText = "#f5deb3",        // 小麦色文字
                DrawerIcon = "#daa520",        // 古董金图标
                
                TextPrimary = "#f5deb3",       // 小麦色文字
                TextSecondary = "#daa520",     // 古董金文字
                TextDisabled = "#8b7355",      // 暗卡其色文字
                
                ActionDefault = "#daa520",     // 古董金
                ActionDisabled = "#654321",    // 咖啡色
                ActionDisabledBackground = "#2f1b14", // 深咖啡背景
                
                Divider = "#654321",           // 咖啡色分隔线
                DividerLight = "#8b7355",      // 暗卡其色分隔线
                
                TableLines = "#654321",        // 表格线
                TableStriped = "#2f1b14",      // 表格条纹
                TableHover = "#654321",        // 表格悬停
                
                LinesDefault = "#654321",      // 默认线条
                LinesInputs = "#8b7355",       // 输入框线条
                
                GrayDefault = "#a0522d",       // 默认灰色
                GrayLight = "#ddd5c7",         // 浅灰色
                GrayLighter = "#f5deb3",       // 更浅灰色
                GrayDark = "#654321",          // 深灰色
                GrayDarker = "#2f1b14",        // 更深灰色
                
                OverlayDark = "rgba(26,16,8,0.8)",        // 深黑褐遮罩
                OverlayLight = "rgba(47,27,20,0.6)"       // 深咖啡遮罩
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "4px",
                AppbarHeight = "64px",
                DrawerWidthLeft = "280px",
                DrawerWidthRight = "280px"
            },
            Typography = new Typography()
            {
                Default = new DefaultTypography()
                {
                    FontFamily = new[] { "Courier New", "Monaco", "Lucida Console", "monospace" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.6",
                    LetterSpacing = "0.02em"
                },
                H1 = new H1Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "3rem",
                    FontWeight = "700",
                    LineHeight = "1.2",
                    LetterSpacing = "-0.01562em"
                },
                H2 = new H2Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "2.5rem",
                    FontWeight = "700",
                    LineHeight = "1.25",
                    LetterSpacing = "-0.00833em"
                },
                H3 = new H3Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "2rem",
                    FontWeight = "600",
                    LineHeight = "1.3",
                    LetterSpacing = "0em"
                },
                H4 = new H4Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "1.5rem",
                    FontWeight = "600",
                    LineHeight = "1.35",
                    LetterSpacing = "0.00735em"
                },
                H5 = new H5Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "1.25rem",
                    FontWeight = "600",
                    LineHeight = "1.4",
                    LetterSpacing = "0em"
                },
                H6 = new H6Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "1.125rem",
                    FontWeight = "600",
                    LineHeight = "1.45",
                    LetterSpacing = "0.0075em"
                },
                Button = new ButtonTypography()
                {
                    FontFamily = new[] { "Courier New", "Monaco", "Lucida Console", "monospace" },
                    FontSize = "0.875rem",
                    FontWeight = "600",
                    LineHeight = "1.5",
                    LetterSpacing = "0.05em",
                    TextTransform = "uppercase"
                },
                Body1 = new Body1Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "1rem",
                    FontWeight = "400",
                    LineHeight = "1.7",
                    LetterSpacing = "0.01em"
                },
                Body2 = new Body2Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "0.875rem",
                    FontWeight = "400",
                    LineHeight = "1.65",
                    LetterSpacing = "0.01071em"
                },
                Caption = new CaptionTypography()
                {
                    FontFamily = new[] { "Courier New", "Monaco", "Lucida Console", "monospace" },
                    FontSize = "0.75rem",
                    FontWeight = "400",
                    LineHeight = "1.8",
                    LetterSpacing = "0.05em"
                },
                Subtitle1 = new Subtitle1Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "1rem",
                    FontWeight = "500",
                    LineHeight = "1.75",
                    LetterSpacing = "0.01em"
                },
                Subtitle2 = new Subtitle2Typography()
                {
                    FontFamily = new[] { "Times New Roman", "Georgia", "serif" },
                    FontSize = "0.875rem",
                    FontWeight = "500",
                    LineHeight = "1.6",
                    LetterSpacing = "0.01em"
                },
                Overline = new OverlineTypography()
                {
                    FontFamily = new[] { "Courier New", "Monaco", "Lucida Console", "monospace" },
                    FontSize = "0.75rem",
                    FontWeight = "600",
                    LineHeight = "2.5",
                    LetterSpacing = "0.1em",
                    TextTransform = "uppercase"
                }
            },
            Shadows = new Shadow()
            {
                Elevation = new string[]
                {
                    "none",
                    "0 1px 3px rgba(139, 69, 19, 0.2)",
                    "0 2px 6px rgba(139, 69, 19, 0.25)",
                    "0 3px 9px rgba(139, 69, 19, 0.3)",
                    "0 4px 12px rgba(139, 69, 19, 0.35)",
                    "0 5px 15px rgba(139, 69, 19, 0.4)",
                    "0 6px 18px rgba(139, 69, 19, 0.45)",
                    "0 7px 21px rgba(139, 69, 19, 0.5)",
                    "0 8px 24px rgba(139, 69, 19, 0.55)",
                    "0 9px 27px rgba(139, 69, 19, 0.6)",
                    "0 10px 30px rgba(139, 69, 19, 0.65)",
                    "0 11px 33px rgba(139, 69, 19, 0.7)",
                    "0 12px 36px rgba(139, 69, 19, 0.75)",
                    "0 13px 39px rgba(139, 69, 19, 0.8)",
                    "0 14px 42px rgba(139, 69, 19, 0.85)",
                    "0 15px 45px rgba(139, 69, 19, 0.9)",
                    "0 16px 48px rgba(139, 69, 19, 0.95)",
                    "0 17px 51px rgba(139, 69, 19, 1.0)",
                    "0 18px 54px rgba(139, 69, 19, 1.0)",
                    "0 19px 57px rgba(139, 69, 19, 1.0)",
                    "0 20px 60px rgba(139, 69, 19, 1.0)",
                    "0 21px 63px rgba(139, 69, 19, 1.0)",
                    "0 22px 66px rgba(139, 69, 19, 1.0)",
                    "0 23px 69px rgba(139, 69, 19, 1.0)",
                    "0 24px 72px rgba(139, 69, 19, 1.0)",
                    "0 25px 75px rgba(139, 69, 19, 1.0)"
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