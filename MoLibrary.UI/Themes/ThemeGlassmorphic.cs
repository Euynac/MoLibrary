using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// 毛玻璃主题 - 现代化的毛玻璃效果，具有未来感和科技感
/// </summary>
public class ThemeGlassmorphic : IThemeProvider
{
    public string Name => "glassmorphic";
    public string DisplayName => "毛玻璃主题";
    public string Description => "现代化的毛玻璃效果，具有未来感和科技感";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#667eea",
                PrimaryLighten = "#8b9bf5",
                PrimaryDarken = "#5568d5",
                PrimaryContrastText = "#ffffff",
                Secondary = "#f093fb",
                SecondaryLighten = "#f5b3fd",
                SecondaryDarken = "#d673e6",
                SecondaryContrastText = "#ffffff",
                Tertiary = "#4facfe",
                TertiaryLighten = "#87c5fe",
                TertiaryDarken = "#00f2fe",
                TertiaryContrastText = "#ffffff",
                Info = "#4299e1",
                InfoLighten = "#7bb3f0",
                InfoDarken = "#2d7dd2",
                InfoContrastText = "#ffffff",
                Success = "#48bb78",
                SuccessLighten = "#68d391",
                SuccessDarken = "#38a169",
                SuccessContrastText = "#ffffff",
                Warning = "#ed8936",
                WarningLighten = "#f6ad55",
                WarningDarken = "#dd6b20",
                WarningContrastText = "#ffffff",
                Error = "#f56565",
                ErrorLighten = "#fc8181",
                ErrorDarken = "#e53e3e",
                ErrorContrastText = "#ffffff",
                Dark = "#1a202c",
                DarkLighten = "#2d3748",
                DarkDarken = "#171923",
                DarkContrastText = "#ffffff",
                TextPrimary = "rgba(26, 32, 44, 0.9)",
                TextSecondary = "rgba(74, 85, 104, 0.8)",
                TextDisabled = "rgba(160, 174, 192, 0.6)",
                ActionDefault = "rgba(26, 32, 44, 0.87)",
                ActionDisabled = "rgba(160, 174, 192, 0.3)",
                ActionDisabledBackground = "rgba(160, 174, 192, 0.12)",
                Background = "rgba(248, 249, 255, 0)",
                BackgroundGray = "rgba(248, 249, 255, 0.6)",
                Surface = "rgba(255, 255, 255, 0.25)",
                DrawerBackground = "rgba(255, 255, 255, 0.85)",
                DrawerText = "rgba(26, 32, 44, 0.9)",
                DrawerIcon = "rgba(74, 85, 104, 0.7)",
                AppbarBackground = "rgba(255, 255, 255, 0.3)",
                AppbarText = "rgba(26, 32, 44, 0.9)",
                LinesDefault = "rgba(255, 255, 255, 0.18)",
                LinesInputs = "rgba(103, 126, 234, 0.3)",
                TableLines = "rgba(255, 255, 255, 0.18)",
                TableStriped = "rgba(103, 126, 234, 0.02)",
                TableHover = "rgba(103, 126, 234, 0.04)",
                Divider = "rgba(255, 255, 255, 0.18)",
                DividerLight = "rgba(255, 255, 255, 0.12)",
                HoverOpacity = 0.04
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#00d4ff",
                PrimaryLighten = "#4de0ff",
                PrimaryDarken = "#00a8cc",
                PrimaryContrastText = "#000000",
                Secondary = "#ff006e",
                SecondaryLighten = "#ff4d96",
                SecondaryDarken = "#cc0056",
                SecondaryContrastText = "#ffffff",
                Tertiary = "#00ff88",
                TertiaryLighten = "#4dffaa",
                TertiaryDarken = "#00cc6a",
                TertiaryContrastText = "#000000",
                Info = "#00d4ff",
                InfoLighten = "#4de0ff",
                InfoDarken = "#00a8cc",
                InfoContrastText = "#000000",
                Success = "#00ff88",
                SuccessLighten = "#4dffaa",
                SuccessDarken = "#00cc6a",
                SuccessContrastText = "#000000",
                Warning = "#ffaa00",
                WarningLighten = "#ffcc4d",
                WarningDarken = "#cc8800",
                WarningContrastText = "#000000",
                Error = "#ff0055",
                ErrorLighten = "#ff4d82",
                ErrorDarken = "#cc0044",
                ErrorContrastText = "#ffffff",
                Dark = "#0a0e27",
                DarkLighten = "#1a1e37",
                DarkDarken = "#050711",
                DarkContrastText = "#ffffff",
                TextPrimary = "rgba(255, 255, 255, 0.95)",
                TextSecondary = "rgba(255, 255, 255, 0.7)",
                TextDisabled = "rgba(255, 255, 255, 0.5)",
                ActionDefault = "rgba(255, 255, 255, 0.87)",
                ActionDisabled = "rgba(255, 255, 255, 0.3)",
                ActionDisabledBackground = "rgba(255, 255, 255, 0.12)",
                Background = "rgba(10, 14, 39, 0)",
                BackgroundGray = "rgba(13, 17, 40, 0.6)",
                Surface = "rgba(13, 17, 40, 0.4)",
                DrawerBackground = "rgba(13, 17, 40, 0.85)",
                DrawerText = "rgba(255, 255, 255, 0.95)",
                DrawerIcon = "rgba(255, 255, 255, 0.7)",
                AppbarBackground = "rgba(13, 17, 40, 0.3)",
                AppbarText = "rgba(255, 255, 255, 0.95)",
                LinesDefault = "rgba(255, 255, 255, 0.08)",
                LinesInputs = "rgba(255, 255, 255, 0.3)",
                TableLines = "rgba(255, 255, 255, 0.08)",
                TableStriped = "rgba(0, 212, 255, 0.02)",
                TableHover = "rgba(0, 212, 255, 0.08)",
                Divider = "rgba(255, 255, 255, 0.08)",
                DividerLight = "rgba(255, 255, 255, 0.04)",
                HoverOpacity = 0.08
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "12px",
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "260px",
                DrawerMiniWidthLeft = "68px",
                DrawerMiniWidthRight = "68px",
                AppbarHeight = "64px"
            },
            Typography = CreateDefaultTypography(),
            Shadows = new Shadow(),
            ZIndex = new ZIndex()
        };
    }

    private static Typography CreateDefaultTypography()
    {
        return new Typography()
        {
            Default = new DefaultTypography()
            {
                FontFamily = new[] { "Roboto", "Helvetica", "Arial", "sans-serif" },
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.43",
                LetterSpacing = "0.01071em"
            },
            H1 = new H1Typography()
            {
                FontSize = "6rem",
                FontWeight = "300",
                LineHeight = "1.167",
                LetterSpacing = "-0.01562em"
            },
            H2 = new H2Typography()
            {
                FontSize = "3.75rem",
                FontWeight = "300",
                LineHeight = "1.2",
                LetterSpacing = "-0.00833em"
            },
            H3 = new H3Typography()
            {
                FontSize = "3rem",
                FontWeight = "400",
                LineHeight = "1.167",
                LetterSpacing = "0em"
            },
            H4 = new H4Typography()
            {
                FontSize = "2.125rem",
                FontWeight = "400",
                LineHeight = "1.235",
                LetterSpacing = "0.00735em"
            },
            H5 = new H5Typography()
            {
                FontSize = "1.5rem",
                FontWeight = "400",
                LineHeight = "1.334",
                LetterSpacing = "0em"
            },
            H6 = new H6Typography()
            {
                FontSize = "1.25rem",
                FontWeight = "500",
                LineHeight = "1.6",
                LetterSpacing = "0.0075em"
            },
            Subtitle1 = new Subtitle1Typography()
            {
                FontSize = "1rem",
                FontWeight = "400",
                LineHeight = "1.75",
                LetterSpacing = "0.00938em"
            },
            Subtitle2 = new Subtitle2Typography()
            {
                FontSize = "0.875rem",
                FontWeight = "500",
                LineHeight = "1.57",
                LetterSpacing = "0.00714em"
            },
            Body1 = new Body1Typography()
            {
                FontSize = "1rem",
                FontWeight = "400",
                LineHeight = "1.5",
                LetterSpacing = "0.00938em"
            },
            Body2 = new Body2Typography()
            {
                FontSize = "0.875rem",
                FontWeight = "400",
                LineHeight = "1.43",
                LetterSpacing = "0.01071em"
            },
            Button = new ButtonTypography()
            {
                FontSize = "0.875rem",
                FontWeight = "500",
                LineHeight = "1.75",
                LetterSpacing = "0.02857em",
                TextTransform = "none"
            },
            Caption = new CaptionTypography()
            {
                FontSize = "0.75rem",
                FontWeight = "400",
                LineHeight = "1.66",
                LetterSpacing = "0.03333em"
            },
            Overline = new OverlineTypography()
            {
                FontSize = "0.75rem",
                FontWeight = "400",
                LineHeight = "2.66",
                LetterSpacing = "0.08333em",
                TextTransform = "uppercase"
            }
        };
    }
}