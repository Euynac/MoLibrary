using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 霓虹脉冲主题 - 具有现代科技感的动态主题
/// </summary>
public class ThemeNeonPulse : IThemeProvider
{
    public string Name => "neonpulse";
    
    public string DisplayName => "霓虹脉冲";
    
    public string Description => "充满活力的霓虹灯效果，带有动态脉冲动画和赛博朋克风格";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#0ea5e9",          // 明亮的天蓝色
                Secondary = "#a855f7",        // 鲜艳的紫色
                Tertiary = "#10b981",         // 翠绿色
                Info = "#06b6d4",             // 青色
                Success = "#22c55e",          // 绿色
                Warning = "#f59e0b",          // 橙色
                Error = "#ef4444",            // 红色
                Dark = "#1e293b",             // 深灰蓝
                Background = "#f8fafc",       // 极浅灰
                BackgroundGray = "#e2e8f0",   // 浅灰
                Surface = "#ffffff",          // 纯白
                DrawerBackground = "#f1f5f9", // 抽屉背景
                DrawerText = "#334155",       // 抽屉文字
                DrawerIcon = "#64748b",       // 抽屉图标
                AppbarBackground = "#ffffff", // 应用栏背景
                AppbarText = "#1e293b",       // 应用栏文字
                TextPrimary = "#1e293b",      // 主文字
                TextSecondary = "#64748b",    // 次要文字
                ActionDefault = "#94a3b8",    // 默认动作
                ActionDisabled = "#cbd5e1",   // 禁用动作
                ActionDisabledBackground = "#f1f5f9", // 禁用背景
                Divider = "#e2e8f0",          // 分割线
                DividerLight = "#f1f5f9",     // 浅分割线
                TableLines = "#e2e8f0",       // 表格线
                LinesDefault = "#e2e8f0",     // 默认线条
                LinesInputs = "#cbd5e1",      // 输入框线条
                TextDisabled = "#94a3b8",     // 禁用文字
                PrimaryDarken = "#0284c7",    // 主色深色
                PrimaryLighten = "#38bdf8",   // 主色浅色
                SecondaryDarken = "#9333ea",  // 次色深色
                SecondaryLighten = "#c084fc", // 次色浅色
                TertiaryDarken = "#059669",   // 第三色深色
                TertiaryLighten = "#34d399",  // 第三色浅色
                InfoDarken = "#0891b2",       // 信息色深色
                InfoLighten = "#22d3ee",      // 信息色浅色
                SuccessDarken = "#16a34a",    // 成功色深色
                SuccessLighten = "#4ade80",   // 成功色浅色
                WarningDarken = "#d97706",    // 警告色深色
                WarningLighten = "#fbbf24",   // 警告色浅色
                ErrorDarken = "#dc2626",      // 错误色深色
                ErrorLighten = "#f87171",     // 错误色浅色
                DarkDarken = "#0f172a",       // 深色深色
                DarkLighten = "#334155",      // 深色浅色
                PrimaryContrastText = "#ffffff", // 主色对比文字
                SecondaryContrastText = "#ffffff", // 次色对比文字
                TertiaryContrastText = "#ffffff", // 第三色对比文字
                InfoContrastText = "#ffffff",  // 信息色对比文字
                SuccessContrastText = "#ffffff", // 成功色对比文字
                WarningContrastText = "#000000", // 警告色对比文字
                ErrorContrastText = "#ffffff", // 错误色对比文字
                DarkContrastText = "#ffffff",  // 深色对比文字
                OverlayDark = "rgba(15, 23, 42, 0.8)", // 深色遮罩
                OverlayLight = "rgba(241, 245, 249, 0.8)" // 浅色遮罩
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#00d4ff",          // 霓虹青蓝
                Secondary = "#ff006e",        // 霓虹粉红
                Tertiary = "#00ff88",         // 霓虹绿
                Info = "#00bcd4",             // 信息青
                Success = "#00e676",          // 成功绿
                Warning = "#ff9100",          // 警告橙
                Error = "#ff1744",            // 错误红
                Dark = "#0a0e27",             // 极深蓝
                Background = "#0f1419",       // 深背景
                BackgroundGray = "#1a1f2e",   // 深灰背景
                Surface = "#151922",          // 表面
                DrawerBackground = "#0f1419", // 抽屉背景
                DrawerText = "#e2e8f0",       // 抽屉文字
                DrawerIcon = "#94a3b8",       // 抽屉图标
                AppbarBackground = "#0a0e27", // 应用栏背景
                AppbarText = "#f1f5f9",       // 应用栏文字
                TextPrimary = "#f1f5f9",      // 主文字
                TextSecondary = "#94a3b8",    // 次要文字
                ActionDefault = "#64748b",    // 默认动作
                ActionDisabled = "#334155",   // 禁用动作
                ActionDisabledBackground = "#1e293b", // 禁用背景
                Divider = "#334155",          // 分割线
                DividerLight = "#475569",     // 浅分割线
                TableLines = "#334155",       // 表格线
                LinesDefault = "#334155",     // 默认线条
                LinesInputs = "#475569",      // 输入框线条
                TextDisabled = "#64748b",     // 禁用文字
                PrimaryDarken = "#0099cc",    // 主色深色
                PrimaryLighten = "#33ddff",   // 主色浅色
                SecondaryDarken = "#cc0058",  // 次色深色
                SecondaryLighten = "#ff3385", // 次色浅色
                TertiaryDarken = "#00cc6a",   // 第三色深色
                TertiaryLighten = "#33ffa3",  // 第三色浅色
                InfoDarken = "#00acc1",       // 信息色深色
                InfoLighten = "#26c6da",      // 信息色浅色
                SuccessDarken = "#00c853",    // 成功色深色
                SuccessLighten = "#69f0ae",   // 成功色浅色
                WarningDarken = "#ff6d00",    // 警告色深色
                WarningLighten = "#ffab40",   // 警告色浅色
                ErrorDarken = "#d50000",      // 错误色深色
                ErrorLighten = "#ff5252",     // 错误色浅色
                DarkDarken = "#050712",       // 深色深色
                DarkLighten = "#1e293b",      // 深色浅色
                PrimaryContrastText = "#000000", // 主色对比文字
                SecondaryContrastText = "#ffffff", // 次色对比文字
                TertiaryContrastText = "#000000", // 第三色对比文字
                InfoContrastText = "#000000",  // 信息色对比文字
                SuccessContrastText = "#000000", // 成功色对比文字
                WarningContrastText = "#000000", // 警告色对比文字
                ErrorContrastText = "#ffffff", // 错误色对比文字
                DarkContrastText = "#ffffff",  // 深色对比文字
                OverlayDark = "rgba(5, 7, 18, 0.9)", // 深色遮罩
                OverlayLight = "rgba(30, 41, 59, 0.5)" // 浅色遮罩
            },
        
        };
    }
}