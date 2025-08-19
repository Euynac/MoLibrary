using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 墨韵山水主题 - 中国水墨画风格主题
/// </summary>
public class ThemeInkLandscape : ThemeBase
{
    public override string Name => "ink-landscape";
    public override string DisplayName => "墨韵山水";
    public override string Description => "中国水墨画风格主题。以黑白灰为主调，点缀淡雅的青绿或赭石色。使用留白设计，配合毛笔笔触效果的分割线和按钮。适合文化类、阅读类应用。";
    
    public override CodeBlockTheme LightCodeBlockTheme => CodeBlockTheme.Ascetic;
    public override CodeBlockTheme DarkCodeBlockTheme => CodeBlockTheme.AtomOneDark;

    public override MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#1a1d23",           // 浓墨（更深邃的主色）
                Secondary = "#374151",         // 深灰墨（更有层次）
                Tertiary = "#6b7280",          // 中灰墨（平衡过渡）
                Info = "#0f766e",              // 墨绿（传统青绿，更沉稳）
                Success = "#059669",           // 竹绿（象征生机）
                Warning = "#ca8a04",           // 赭黄（传统矿物色）
                Error = "#dc2626",             // 朱砂红（传统颜料色）
                Dark = "#0f172a",              // 极浓墨
                
                Background = "#faf9f7",        // 宣纸白（微黄调，更自然）
                BackgroundGray = "#f3f4f6",    // 淡灰（更柔和）
                Surface = "#ffffff",           // 纯白
                AppbarBackground = "#1a1d23",  // 浓墨导航
                AppbarText = "#f9fafb",        // 素白文字
                DrawerBackground = "#faf9f7",  // 宣纸白抽屉
                DrawerText = "#1a1d23",        // 浓墨文字
                DrawerIcon = "#374151",        // 深灰墨图标
                
                TextPrimary = "#0f172a",       // 极浓墨文字（增强对比）
                TextSecondary = "#374151",     // 深灰墨文字
                TextDisabled = "#9ca3af",      // 浅灰墨文字（更柔和）
                
                ActionDefault = "#f3f4f6",     // 淡灰（更适合默认行为）
                ActionDisabled = "#e5e7eb",    // 极浅灰
                ActionDisabledBackground = "#f9fafb", // 淡背景
                
                Divider = "#d1d5db",           // 淡墨线（更自然）
                DividerLight = "#e5e7eb",      // 极淡墨线
                
                TableLines = "#d1d5db",        // 表格墨线
                TableStriped = "#f9fafb",      // 表格条纹（更淡雅）
                TableHover = "#f3f4f6",        // 表格悬停
                
                LinesDefault = "#d1d5db",      // 默认墨线
                LinesInputs = "#9ca3af",       // 输入框墨线（更清晰）
                
                GrayDefault = "#6b7280",       // 默认灰墨
                GrayLight = "#9ca3af",         // 浅灰墨
                GrayLighter = "#d1d5db",       // 更浅灰墨
                GrayDark = "#374151",          // 深灰墨
                GrayDarker = "#1a1d23",        // 更深灰墨
                
                OverlayDark = "rgba(15,23,42,0.4)",    // 深墨遮罩（更柔和）
                OverlayLight = "rgba(255,255,255,0.85)"  // 浅色遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#06b6d4",           // 青蓝（传统青绿色调，暗色下有足够对比度）
                Secondary = "#64748b",         // 中灰墨（更有层次）
                Tertiary = "#475569",          // 深灰墨
                Info = "#14b8a6",              // 青绿（暗色下更亮）
                Success = "#10b981",           // 翠绿（保持生机）
                Warning = "#f59e0b",           // 赭黄（暗色下更温暖）
                Error = "#ef4444",             // 朱红（暗色下保持警示）
                Dark = "#f9fafb",              // 素白
                
                Background = "#0f172a",        // 极深墨背景（更深邃）
                BackgroundGray = "#1e293b",    // 深墨背景
                Surface = "#1e293b",           // 深墨表面
                AppbarBackground = "#0f172a",  // 极深墨导航
                AppbarText = "#f1f5f9",        // 素白文字
                DrawerBackground = "#1e293b",  // 深墨抽屉
                DrawerText = "#f1f5f9",        // 素白文字
                DrawerIcon = "#94a3b8",        // 中灰图标
                
                TextPrimary = "#f8fafc",       // 纯白文字（增强对比）
                TextSecondary = "#cbd5e1",     // 淡灰文字
                TextDisabled = "#64748b",      // 中灰墨文字
                
                ActionDefault = "#475569",     // 中灰（适合暗色默认行为）
                ActionDisabled = "#64748b",    // 浅灰
                ActionDisabledBackground = "#1e293b", // 深背景
                
                Divider = "#475569",           // 深灰墨分隔线（更清晰）
                DividerLight = "#64748b",      // 中灰墨分隔线
                
                TableLines = "#475569",        // 表格墨线
                TableStriped = "#1e293b",      // 表格条纹
                TableHover = "#334155",        // 表格悬停（更明显）
                
                LinesDefault = "#475569",      // 默认墨线
                LinesInputs = "#64748b",       // 输入框墨线（更清晰）
                
                GrayDefault = "#64748b",       // 默认灰墨
                GrayLight = "#94a3b8",         // 浅灰墨
                GrayLighter = "#cbd5e1",       // 更浅灰墨
                GrayDark = "#475569",          // 深灰墨
                GrayDarker = "#334155",        // 更深灰墨
                
                OverlayDark = "rgba(15,23,42,0.8)",    // 极深墨遮罩
                OverlayLight = "rgba(30,41,59,0.6)"    // 深墨遮罩
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
                    "none",                                                    // 0: 无阴影
                    "0 1px 2px rgba(15, 23, 42, 0.08)",                      // 1: 微妙墨迹
                    "0 1px 3px rgba(15, 23, 42, 0.1), 0 1px 2px rgba(15, 23, 42, 0.06)", // 2: 轻微层次
                    "0 2px 4px rgba(15, 23, 42, 0.1), 0 2px 3px rgba(15, 23, 42, 0.06)", // 3: 淡墨阴影
                    "0 2px 6px rgba(15, 23, 42, 0.12), 0 2px 4px rgba(15, 23, 42, 0.08)", // 4: 标准墨影
                    "0 4px 8px rgba(15, 23, 42, 0.12), 0 2px 4px rgba(15, 23, 42, 0.08)", // 5: 明显层次
                    "0 6px 12px rgba(15, 23, 42, 0.15), 0 2px 4px rgba(15, 23, 42, 0.08)", // 6: 卡片阴影
                    "0 8px 16px rgba(15, 23, 42, 0.15), 0 2px 6px rgba(15, 23, 42, 0.08)", // 7: 浮起效果
                    "0 10px 20px rgba(15, 23, 42, 0.15), 0 4px 8px rgba(15, 23, 42, 0.08)", // 8: 对话框
                    "0 12px 24px rgba(15, 23, 42, 0.15), 0 4px 8px rgba(15, 23, 42, 0.08)", // 9: 深度层次
                    "0 16px 32px rgba(15, 23, 42, 0.15), 0 4px 8px rgba(15, 23, 42, 0.08)", // 10: 重要内容
                    "0 20px 40px rgba(15, 23, 42, 0.15), 0 4px 8px rgba(15, 23, 42, 0.08)", // 11: 抽屉效果
                    "0 24px 48px rgba(15, 23, 42, 0.15), 0 6px 12px rgba(15, 23, 42, 0.08)", // 12: 模态框
                    "0 28px 56px rgba(15, 23, 42, 0.15), 0 6px 12px rgba(15, 23, 42, 0.08)", // 13: 悬浮面板
                    "0 32px 64px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 14: 最高层级
                    "0 36px 72px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 15: 极高层级
                    "0 40px 80px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 16: 特殊效果
                    "0 44px 88px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 17: 超高层级
                    "0 48px 96px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 18: 最大层级
                    "0 52px 104px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 19: 扩展层级
                    "0 56px 112px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 20: 特殊用途
                    "0 60px 120px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 21: 自定义1
                    "0 64px 128px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 22: 自定义2
                    "0 68px 136px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 23: 自定义3
                    "0 72px 144px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)", // 24: 自定义4
                    "0 76px 152px rgba(15, 23, 42, 0.15), 0 8px 16px rgba(15, 23, 42, 0.08)"  // 25: 自定义5
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