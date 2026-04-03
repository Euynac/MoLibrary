using System.Reflection;

namespace Monica.Tool.Runtime;

/// <summary>
/// Provides helpers for resolving paths relative to the current application entry assembly.
/// </summary>
public static class RuntimePathHelper
{
    /// <summary>
    /// Resolves a relative path from the current application running directory.
    /// </summary>
    public static string GetRelativePathInRunningPath(string relativePath)
    {
        return Path.Combine(GetRunningPath(), relativePath);
    }

    /// <summary>
    /// Get the running path of the current application.
    /// </summary>
    /// <returns></returns>
    public static string GetRunningPath()
    {
        return Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
    }
}
