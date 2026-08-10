using MudBlazor;

namespace Monica.UI.Theming;

/// <summary>
/// Provides Monica's offline-safe baseline typography for themes that do not define a type system.
/// </summary>
internal static class MonicaTypographyDefaults
{
    private static readonly string[] BodyFontFamily =
    [
        "MoDefaultBody", "IBM Plex Sans", "Noto Sans SC", "Microsoft YaHei UI", "PingFang SC", "sans-serif"
    ];

    private static readonly string[] DisplayFontFamily =
    [
        "MoDefaultDisplay", "Oxanium", "MoDefaultBody", "IBM Plex Sans", "Noto Sans SC", "Microsoft YaHei UI", "PingFang SC", "sans-serif"
    ];

    internal static Typography CreatePrecision()
    {
        return new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = CloneBodyFamily(),
                FontSize = "0.9375rem",
                FontWeight = "400",
                LineHeight = "1.5",
                LetterSpacing = "0"
            },
            H1 = new H1Typography { FontFamily = CloneDisplayFamily(), FontSize = "2.5rem", FontWeight = "650", LineHeight = "1.08", LetterSpacing = "-0.035em" },
            H2 = new H2Typography { FontFamily = CloneDisplayFamily(), FontSize = "2rem", FontWeight = "650", LineHeight = "1.12", LetterSpacing = "-0.03em" },
            H3 = new H3Typography { FontFamily = CloneDisplayFamily(), FontSize = "1.625rem", FontWeight = "650", LineHeight = "1.18", LetterSpacing = "-0.025em" },
            H4 = new H4Typography { FontFamily = CloneDisplayFamily(), FontSize = "1.375rem", FontWeight = "650", LineHeight = "1.25", LetterSpacing = "-0.02em" },
            H5 = new H5Typography { FontFamily = CloneDisplayFamily(), FontSize = "1.125rem", FontWeight = "650", LineHeight = "1.35", LetterSpacing = "-0.012em" },
            H6 = new H6Typography { FontFamily = CloneDisplayFamily(), FontSize = "1rem", FontWeight = "650", LineHeight = "1.4", LetterSpacing = "-0.006em" },
            Subtitle1 = new Subtitle1Typography { FontFamily = CloneDisplayFamily(), FontSize = "1rem", FontWeight = "600", LineHeight = "1.5", LetterSpacing = "0" },
            Subtitle2 = new Subtitle2Typography { FontFamily = CloneBodyFamily(), FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "0" },
            Body1 = new Body1Typography { FontFamily = CloneBodyFamily(), FontSize = "0.95rem", FontWeight = "400", LineHeight = "1.55", LetterSpacing = "0" },
            Body2 = new Body2Typography { FontFamily = CloneBodyFamily(), FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "0" },
            Button = new ButtonTypography { FontFamily = CloneBodyFamily(), FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.25", LetterSpacing = "0", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = CloneBodyFamily(), FontSize = "0.75rem", FontWeight = "400", LineHeight = "1.35", LetterSpacing = "0.01em" },
            Overline = new OverlineTypography { FontFamily = CloneDisplayFamily(), FontSize = "0.6875rem", FontWeight = "600", LineHeight = "1.25", LetterSpacing = "0.09em", TextTransform = "uppercase" }
        };
    }

    internal static Typography CreateFallback()
    {
        var typography = new Typography();

        typography.Default.FontFamily = CloneBodyFamily();
        typography.H1.FontFamily = CloneDisplayFamily();
        typography.H2.FontFamily = CloneDisplayFamily();
        typography.H3.FontFamily = CloneDisplayFamily();
        typography.H4.FontFamily = CloneDisplayFamily();
        typography.H5.FontFamily = CloneDisplayFamily();
        typography.H6.FontFamily = CloneDisplayFamily();
        typography.Subtitle1.FontFamily = CloneDisplayFamily();
        typography.Subtitle2.FontFamily = CloneBodyFamily();
        typography.Body1.FontFamily = CloneBodyFamily();
        typography.Body2.FontFamily = CloneBodyFamily();
        typography.Button.FontFamily = CloneBodyFamily();
        typography.Caption.FontFamily = CloneBodyFamily();
        typography.Overline.FontFamily = CloneDisplayFamily();

        return typography;
    }

    private static string[] CloneBodyFamily() => [.. BodyFontFamily];

    private static string[] CloneDisplayFamily() => [.. DisplayFontFamily];
}
