using System.Runtime.InteropServices;
using Monica.DevOps.Terminal.Models;

namespace Monica.DevOps.Terminal.Services.Support;

/// <summary>
/// Detects local operating system and shell defaults for the terminal module.
/// </summary>
public sealed class TerminalEnvironmentDetector
{
    /// <summary>
    /// Gets the current terminal execution environment.
    /// </summary>
    /// <returns>The detected environment information.</returns>
    public TerminalEnvironmentInfo Detect()
    {
        var shell = ResolveShell();
        return new TerminalEnvironmentInfo
        {
            OperatingSystemFamily = GetOperatingSystemFamily(),
            OperatingSystemDescription = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            ShellPath = shell.Path,
            ShellArguments = [.. shell.Arguments],
            DefaultWorkingDirectory = Directory.GetCurrentDirectory(),
            ApplicationBaseDirectory = AppContext.BaseDirectory
        };
    }

    private static string GetOperatingSystemFamily()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        return "Unknown";
    }

    private static TerminalShell ResolveShell()
    {
        if (OperatingSystem.IsWindows())
        {
            return CommandExists("powershell.exe")
                ? new TerminalShell("powershell.exe", ["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"])
                : new TerminalShell("cmd.exe", ["/C"]);
        }

        var configuredShell = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(configuredShell) && File.Exists(configuredShell))
        {
            return new TerminalShell(configuredShell, ["-lc"]);
        }

        if (File.Exists("/bin/bash"))
        {
            return new TerminalShell("/bin/bash", ["-lc"]);
        }

        return new TerminalShell("/bin/sh", ["-c"]);
    }

    private static bool CommandExists(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, command))
            .Any(File.Exists);
    }

    private sealed record TerminalShell(string Path, IReadOnlyList<string> Arguments);
}
