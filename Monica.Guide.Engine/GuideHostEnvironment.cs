namespace Monica.Guide;

/// <summary>Host kinds whose user configuration paths differ.</summary>
public enum GuideHostPlatform
{
    Windows,
    Wsl,
    Linux,
    MacOS
}

/// <summary>Testable view of the operating-system environment used by guide planning.</summary>
public interface IGuideHostEnvironment
{
    GuideHostPlatform Platform { get; }

    string UserHomeDirectory { get; }

    string LocalApplicationDataDirectory { get; }

    string? GetEnvironmentVariable(string name);
}

/// <summary>Current-process host environment.</summary>
public sealed class GuideHostEnvironment : IGuideHostEnvironment
{
    public GuideHostPlatform Platform { get; } = DetectPlatform();

    public string UserHomeDirectory { get; } = ResolveHomeDirectory();

    public string LocalApplicationDataDirectory { get; } = ResolveLocalApplicationData();

    public string? GetEnvironmentVariable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Environment.GetEnvironmentVariable(name);
    }

    private static GuideHostPlatform DetectPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return GuideHostPlatform.Windows;
        }

        if (OperatingSystem.IsLinux()
            && (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WSL_DISTRO_NAME"))
                || File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop")))
        {
            return GuideHostPlatform.Wsl;
        }

        return OperatingSystem.IsMacOS()
            ? GuideHostPlatform.MacOS
            : GuideHostPlatform.Linux;
    }

    private static string ResolveHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME");
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            throw new InvalidOperationException("The current user home directory could not be resolved.");
        }

        return Path.GetFullPath(home);
    }

    private static string ResolveLocalApplicationData()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            return Path.GetFullPath(local);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrWhiteSpace(xdg)
            ? Path.GetFullPath(xdg)
            : Path.Combine(ResolveHomeDirectory(), ".local", "share");
    }
}

/// <summary>Path conversion helpers used when a WSL shell launches the Windows program.</summary>
public static class GuideHostPath
{
    public static bool TryConvertWslPathToWindows(string path, out string windowsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/');
        if (normalized.Length > 7
            && normalized.StartsWith("/mnt/", StringComparison.Ordinal)
            && char.IsAsciiLetter(normalized[5])
            && normalized[6] == '/')
        {
            windowsPath = $"{char.ToUpperInvariant(normalized[5])}:\\{normalized[7..].Replace('/', '\\')}";
            return true;
        }

        windowsPath = string.Empty;
        return false;
    }

    public static bool TryConvertWindowsPathToWsl(string path, out string wslPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/')
        {
            wslPath = $"/mnt/{char.ToLowerInvariant(path[0])}/{path[3..].Replace('\\', '/')}";
            return true;
        }

        wslPath = string.Empty;
        return false;
    }
}
