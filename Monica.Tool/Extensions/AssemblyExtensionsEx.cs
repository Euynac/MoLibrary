using System.Reflection;

namespace Monica.Tool.Extensions;

public static class AssemblyExtensionsEx
{
    /// <summary>
    /// Gets the version number of the assembly.
    /// <br/>English: Get the version number of the assembly.
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static Version? GetVersion(this Assembly assembly) => assembly.GetName().Version;
}