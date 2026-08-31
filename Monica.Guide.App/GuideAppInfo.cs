using System.Reflection;

namespace Monica.Guide.App;

/// <summary>Identity and version metadata of the Monica Guide application.</summary>
public static class GuideAppInfo
{
    private static readonly Lazy<string> CURRENT_VERSION = new(ResolveCurrentVersion);

    /// <summary>The build-supplied assembly informational version.</summary>
    public static string CurrentVersion => CURRENT_VERSION.Value;

    private static string ResolveCurrentVersion()
    {
        var assembly = typeof(GuideAppInfo).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "unknown";
    }
}
