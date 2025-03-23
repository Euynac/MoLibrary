using System;
using System.Reflection;

namespace MoLibrary.Tool.Extensions;

public static class AssemblyExtensions
{
    /// <summary>
    /// 获取程序集的版本号。
    /// <br/>English: Get the version number of the assembly.
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    public static Version? GetVersion(this Assembly assembly) => assembly.GetName().Version;
}