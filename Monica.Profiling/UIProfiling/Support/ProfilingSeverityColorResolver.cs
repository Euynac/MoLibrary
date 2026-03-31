using MudBlazor;

namespace Monica.Profiling.UIProfiling.Support;

internal static class ProfilingSeverityColorResolver
{
    public static Color GetCpuColor(double cpuPercent)
    {
        if (cpuPercent < 50)
        {
            return Color.Success;
        }

        if (cpuPercent < 80)
        {
            return Color.Warning;
        }

        return Color.Error;
    }

    public static Color GetGcTimeColor(double gcTimePercent)
    {
        if (gcTimePercent < 5)
        {
            return Color.Success;
        }

        if (gcTimePercent < 15)
        {
            return Color.Warning;
        }

        return Color.Error;
    }

    public static Color GetGenerationColor(int generation) => generation switch
    {
        0 => Color.Primary,
        1 => Color.Info,
        2 => Color.Warning,
        3 => Color.Error,
        4 => Color.Dark,
        _ => Color.Default
    };

    public static Color GetFragmentationColor(double percent)
    {
        if (percent < 10)
        {
            return Color.Success;
        }

        if (percent < 25)
        {
            return Color.Warning;
        }

        return Color.Error;
    }

    public static Color GetMemoryLoadColor(double percent)
    {
        if (percent < 70)
        {
            return Color.Success;
        }

        if (percent < 85)
        {
            return Color.Warning;
        }

        return Color.Error;
    }

    public static Color GetPercentageColor(double percentage)
    {
        if (percentage >= 20)
        {
            return Color.Error;
        }

        if (percentage >= 10)
        {
            return Color.Warning;
        }

        return Color.Primary;
    }

    public static string GetCpuClass(double percent)
    {
        if (percent < 50)
        {
            return string.Empty;
        }

        if (percent < 80)
        {
            return "mud-warning-text";
        }

        return "mud-error-text";
    }

    public static string GetGcTimeClass(double percent)
    {
        if (percent < 5)
        {
            return string.Empty;
        }

        if (percent < 15)
        {
            return "mud-warning-text";
        }

        return "mud-error-text";
    }
}
