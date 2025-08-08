using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 小清新主题：清爽、简洁、柔和的色彩搭配
/// 灵感来源于春天的自然色彩和现代简约设计
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
                // 主色调：薄荷绿
                Primary = "#00c896",
                PrimaryLighten = "#33d4aa",
                PrimaryDarken = "#00a87d",
                PrimaryContrastText = "#ffffff",

                // 辅助色：柔和的珊瑚粉
                Secondary = "#ff8a95",
                SecondaryLighten = "#ffb3ba",
                SecondaryDarken = "#ff6b78",
                SecondaryContrastText = "#ffffff",

                // 第三色：天空蓝
                Tertiary = "#85d7ff",
                TertiaryContrastText = "#1e5266",

                // 信息色：清新蓝
                Info = "#64b5f6",
                InfoLighten = "#90caf9",
                InfoDarken = "#42a5f5",
                InfoContrastText = "#ffffff",

                // 成功色：清新绿
                Success = "#66bb6a",
                SuccessLighten = "#81c784",
                SuccessDarken = "#4caf50",
                SuccessContrastText = "#ffffff",

                // 警告色：柔和橙
                Warning = "#ffb74d",
                WarningLighten = "#ffcc80",
                WarningDarken = "#ffa726",
                WarningContrastText = "#1e1e1e",

                // 错误色：柔和红
                Error = "#ff7043",
                ErrorLighten = "#ff8a65",
                ErrorDarken = "#f4511e",
                ErrorContrastText = "#ffffff",

                // 暗色调
                Dark = "#424242",
                DarkLighten = "#616161",
                DarkDarken = "#212121",
                DarkContrastText = "#ffffff",

                // 背景色：非常浅的薄荷色调
                Background = "#f8fffe",
                BackgroundGray = "#f5f7f7",

                // 表面色：纯白带一点点薄荷
                Surface = "#ffffff",
                
                // 抽屉背景
                DrawerBackground = "#fcfffe",
                DrawerText = "#424242",
                DrawerIcon = "#616161",

                // 应用栏背景：清新白
                AppbarBackground = "#ffffff",
                AppbarText = "#424242",

                // 文本色
                TextPrimary = "#2e3440",
                TextSecondary = "#5e6772",
                TextDisabled = "#adb3ba",

                // 操作色
                ActionDefault = "#64b5f6",
                ActionDisabled = "#e0e4e8",
                ActionDisabledBackground = "#f5f7f9",

                // 边框和分割线：非常柔和的灰色
                Divider = "#e8ecef",
                DividerLight = "#f0f3f5",

                // 表格条纹
                TableStriped = "#fafbfb",
                TableHover = "#f0f8f5",

                // 线条
                LinesDefault = "#e0e4e8",
                LinesInputs = "#d0d5da",

                // 覆盖层
                OverlayDark = "rgba(33,33,33,0.3)",
                OverlayLight = "rgba(255,255,255,0.7)",

                // 悬停状态
                HoverOpacity = 0.08,

                // 其他
                GrayDefault = "#9e9e9e",
                GrayLight = "#bdbdbd",
                GrayLighter = "#e0e0e0",
                GrayDark = "#757575",
                GrayDarker = "#616161"
            },
            PaletteDark = new PaletteDark()
            {
                // 主色调：深薄荷绿
                Primary = "#00e5a0",
                PrimaryLighten = "#33eab3",
                PrimaryDarken = "#00c586",
                PrimaryContrastText = "#000000",

                // 辅助色：深珊瑚粉
                Secondary = "#ff9fa8",
                SecondaryLighten = "#ffb8bf",
                SecondaryDarken = "#ff8691",
                SecondaryContrastText = "#000000",

                // 第三色：深天空蓝
                Tertiary = "#9ae3ff",
                TertiaryContrastText = "#003548",

                // 信息色
                Info = "#81d4fa",
                InfoLighten = "#a1defc",
                InfoDarken = "#4fc3f7",
                InfoContrastText = "#000000",

                // 成功色
                Success = "#81c784",
                SuccessLighten = "#a5d6a7",
                SuccessDarken = "#66bb6a",
                SuccessContrastText = "#000000",

                // 警告色
                Warning = "#ffcc80",
                WarningLighten = "#ffd699",
                WarningDarken = "#ffb74d",
                WarningContrastText = "#000000",

                // 错误色
                Error = "#ff8a65",
                ErrorLighten = "#ffab91",
                ErrorDarken = "#ff7043",
                ErrorContrastText = "#000000",

                // 暗色调
                Dark = "#d0d0d0",
                DarkLighten = "#e0e0e0",
                DarkDarken = "#b0b0b0",
                DarkContrastText = "#000000",

                // 背景色：深色带一点绿色调
                Background = "#0f1614",
                BackgroundGray = "#141a18",

                // 表面色：深色表面
                Surface = "#1a211f",
                
                // 抽屉背景
                DrawerBackground = "#161d1b",
                DrawerText = "#e0e0e0",
                DrawerIcon = "#bdbdbd",

                // 应用栏背景
                AppbarBackground = "#1a211f",
                AppbarText = "#e0e0e0",

                // 文本色
                TextPrimary = "#eceff1",
                TextSecondary = "#b0bec5",
                TextDisabled = "#607d8b",

                // 操作色
                ActionDefault = "#81d4fa",
                ActionDisabled = "#455a64",
                ActionDisabledBackground = "#263238",

                // 边框和分割线
                Divider = "#2a3330",
                DividerLight = "#323b38",

                // 表格条纹
                TableStriped = "#1e2624",
                TableHover = "#232b29",

                // 线条
                LinesDefault = "#3a4340",
                LinesInputs = "#455a64",

                // 覆盖层
                OverlayDark = "rgba(0,0,0,0.5)",
                OverlayLight = "rgba(255,255,255,0.1)",

                // 悬停状态
                HoverOpacity = 0.12,

                // 其他
                GrayDefault = "#9e9e9e",
                GrayLight = "#bdbdbd",
                GrayLighter = "#e0e0e0",
                GrayDark = "#757575",
                GrayDarker = "#616161"
            },
           
            LayoutProperties = new LayoutProperties()
            {
                // 使用更大的圆角，营造柔和感
                DefaultBorderRadius = "12px",
                
                // 抽屉宽度
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "260px",
                DrawerMiniWidthLeft = "72px",
                DrawerMiniWidthRight = "72px",
                
                // 应用栏高度
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