using MudBlazor;

namespace MoLibrary.UI.Themes;

/// <summary>
/// MoLibrary默认主题 - 经典的Material Design风格
/// </summary>
public class ThemeMoLibraryDefault : IThemeProvider
{
    public string Name => "default";
    public string DisplayName => "默认主题";
    public string Description => "经典的Material Design风格";

    public MudTheme CreateTheme()
    {
        return new MudTheme()
        {
            PaletteLight = new PaletteLight()
            {
                Primary = "#1976d2",
                PrimaryLighten = "#42a5f5",
                PrimaryDarken = "#1565c0",
                Secondary = "#dc004e",
                SecondaryLighten = "#ff5983",
                SecondaryDarken = "#9a0036",
                Tertiary = "#1976d2",
                TertiaryLighten = "#42a5f5",
                TertiaryDarken = "#1565c0",
                Info = "#2196f3",
                InfoLighten = "#64b5f6",
                InfoDarken = "#1976d2",
                Success = "#4caf50",
                SuccessLighten = "#81c784",
                SuccessDarken = "#388e3c",
                Warning = "#ff9800",
                WarningLighten = "#ffb74d",
                WarningDarken = "#f57c00",
                Error = "#f44336",
                ErrorLighten = "#e57373",
                ErrorDarken = "#d32f2f",
                Dark = "#424242",
                DarkLighten = "#616161",
                DarkDarken = "#212121",
                TextPrimary = "rgba(0, 0, 0, 0.87)",
                TextSecondary = "rgba(0, 0, 0, 0.6)",
                TextDisabled = "rgba(0, 0, 0, 0.38)",
                ActionDefault = "rgba(0, 0, 0, 0.87)",
                ActionDisabled = "rgba(0, 0, 0, 0.26)",
                ActionDisabledBackground = "rgba(0, 0, 0, 0.12)",
                Background = "#fafafa",
                BackgroundGray = "#f5f5f5",
                Surface = "#ffffff",
                DrawerBackground = "#ffffff",
                DrawerText = "rgba(0, 0, 0, 0.87)",
                DrawerIcon = "rgba(0, 0, 0, 0.54)",
                AppbarBackground = "#ffffff",
                AppbarText = "rgba(0, 0, 0, 0.87)",
                LinesDefault = "rgba(0, 0, 0, 0.12)",
                LinesInputs = "rgba(0, 0, 0, 0.42)",
                TableLines = "rgba(0, 0, 0, 0.12)",
                TableStriped = "rgba(0, 0, 0, 0.02)",
                TableHover = "rgba(0, 0, 0, 0.04)",
                Divider = "rgba(0, 0, 0, 0.12)",
                DividerLight = "rgba(0, 0, 0, 0.06)"
            },
            PaletteDark = new PaletteDark()
            {
                Primary = "#90caf9",
                PrimaryLighten = "#e3f2fd",
                PrimaryDarken = "#42a5f5",
                Secondary = "#f48fb1",
                SecondaryLighten = "#fce4ec",
                SecondaryDarken = "#ec407a",
                Tertiary = "#90caf9",
                TertiaryLighten = "#e3f2fd",
                TertiaryDarken = "#42a5f5",
                Info = "#42a5f5",
                InfoLighten = "#90caf9",
                InfoDarken = "#2196f3",
                Success = "#66bb6a",
                SuccessLighten = "#a5d6a7",
                SuccessDarken = "#4caf50",
                Warning = "#ffa726",
                WarningLighten = "#ffcc80",
                WarningDarken = "#ff9800",
                Error = "#ef5350",
                ErrorLighten = "#ef9a9a",
                ErrorDarken = "#e53935",
                Dark = "#424242",
                DarkLighten = "#616161",
                DarkDarken = "#212121",
                TextPrimary = "rgba(255, 255, 255, 0.87)",
                TextSecondary = "rgba(255, 255, 255, 0.7)",
                TextDisabled = "rgba(255, 255, 255, 0.5)",
                ActionDefault = "rgba(255, 255, 255, 0.87)",
                ActionDisabled = "rgba(255, 255, 255, 0.3)",
                ActionDisabledBackground = "rgba(255, 255, 255, 0.12)",
                Background = "#121212",
                BackgroundGray = "#1e1e1e",
                Surface = "#1e1e1e",
                DrawerBackground = "#1e1e1e",
                DrawerText = "rgba(255, 255, 255, 0.87)",
                DrawerIcon = "rgba(255, 255, 255, 0.54)",
                AppbarBackground = "#1e1e1e",
                AppbarText = "rgba(255, 255, 255, 0.87)",
                LinesDefault = "rgba(255, 255, 255, 0.12)",
                LinesInputs = "rgba(255, 255, 255, 0.7)",
                TableLines = "rgba(255, 255, 255, 0.12)",
                TableStriped = "rgba(255, 255, 255, 0.02)",
                TableHover = "rgba(255, 255, 255, 0.08)",
                Divider = "rgba(255, 255, 255, 0.12)",
                DividerLight = "rgba(255, 255, 255, 0.06)"
            },
            LayoutProperties = new LayoutProperties()
            {
                DefaultBorderRadius = "6px",
                DrawerWidthLeft = "260px",
                DrawerWidthRight = "260px",
                DrawerMiniWidthLeft = "68px",
                DrawerMiniWidthRight = "68px",
                AppbarHeight = "64px"
            },
            Typography = new Typography()
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
            },
            Shadows = new Shadow(),
            ZIndex = new ZIndex()
        };
    }
}