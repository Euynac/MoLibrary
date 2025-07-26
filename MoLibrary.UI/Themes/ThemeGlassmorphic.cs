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
                Secondary = "#f093fb",
                SecondaryLighten = "#f5b3fd",
                SecondaryDarken = "#d673e6",
                Tertiary = "#4facfe",
                TertiaryLighten = "#87c5fe",
                TertiaryDarken = "#00f2fe",
                Info = "#4299e1",
                Success = "#48bb78",
                Warning = "#ed8936",
                Error = "#f56565",
                Dark = "#1a202c",
                TextPrimary = "#1a202c",
                TextSecondary = "#4a5568",
                TextDisabled = "#a0aec0",
                Background = "#f8f9ff",
                BackgroundGray = "#f0f2ff",
                Surface = "rgba(255, 255, 255, 0.8)",
                DrawerBackground = "rgba(255, 255, 255, 0.95)",
                AppbarBackground = "rgba(255, 255, 255, 0.85)",
                LinesDefault = "rgba(255, 255, 255, 0.18)",
                TableStriped = "rgba(103, 126, 234, 0.02)",
                TableHover = "rgba(103, 126, 234, 0.04)"
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#00d4ff",
                PrimaryLighten = "#4de0ff",
                PrimaryDarken = "#00a8cc",
                Secondary = "#ff006e",
                SecondaryLighten = "#ff4d96",
                SecondaryDarken = "#cc0056",
                Tertiary = "#00ff88",
                TertiaryLighten = "#4dffaa",
                TertiaryDarken = "#00cc6a",
                Info = "#00d4ff",
                Success = "#00ff88",
                Warning = "#ffaa00",
                Error = "#ff0055",
                Dark = "#0a0e27",
                TextPrimary = "#ffffff",
                TextSecondary = "#a0aec0",
                TextDisabled = "#4a5568",
                Background = "#0a0e27",
                BackgroundGray = "#0d1128",
                Surface = "rgba(13, 17, 40, 0.8)",
                DrawerBackground = "rgba(13, 17, 40, 0.95)",
                AppbarBackground = "rgba(13, 17, 40, 0.85)",
                LinesDefault = "rgba(255, 255, 255, 0.08)",
                TableStriped = "rgba(0, 212, 255, 0.02)",
                TableHover = "rgba(0, 212, 255, 0.08)"
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