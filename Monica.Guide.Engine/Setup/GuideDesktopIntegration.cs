using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Monica.Guide.Setup;

/// <summary>Shell locations the guide can place a product shortcut in.</summary>
public enum GuideShortcutSite
{
    Desktop,
    StartMenu
}

/// <summary>
/// Convenience shell integration for one installed product: desktop/start-menu shortcuts and
/// a per-user logon autostart entry. These are plain user-owned conveniences outside governed
/// guide state, so every mutation is deterministic (fixed names) and removal verifies
/// ownership first.
/// </summary>
public sealed class GuideDesktopIntegration(
    string productDisplayName,
    string autoStartValueName,
    string shortcutArguments,
    string autoStartArguments,
    string? desktopDirectory = null,
    string? programsDirectory = null,
    RegistryKey? runKey = null)
{
    public string ShortcutFileName => $"{productDisplayName}.lnk";
    public string ShortcutDescription => $"{productDisplayName}";
    public string AutoStartValueName => autoStartValueName;

    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _desktopDirectory = desktopDirectory
        ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    private readonly string _programsDirectory = programsDirectory
        ?? Environment.GetFolderPath(Environment.SpecialFolder.Programs);

    /// <summary>Absolute shortcut path for one shell site.</summary>
    public string ShortcutPath(GuideShortcutSite site)
        => Path.Combine(site == GuideShortcutSite.Desktop ? _desktopDirectory : _programsDirectory, ShortcutFileName);

    /// <summary>The exact command line the autostart entry must hold for this executable.</summary>
    public string AutoStartCommand(string executablePath)
        => $"\"{Path.TrimEndingDirectorySeparator(Path.GetFullPath(executablePath))}\" {autoStartArguments}";

    /// <summary>Whether the site holds any shortcut file with the product name.</summary>
    public bool ShortcutExists(GuideShortcutSite site) => File.Exists(ShortcutPath(site));

    private static void ThrowIfNotWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Guide shell integration requires Windows.");
        }
    }

    /// <summary>Creates or replaces the site shortcut so it always points at the given executable.</summary>
    [SupportedOSPlatform("windows")]
    public void CreateShortcut(GuideShortcutSite site, string executablePath)
    {
        ThrowIfNotWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var path = ShortcutPath(site);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(path);
            try
            {
                shortcut.TargetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(executablePath));
                shortcut.Arguments = shortcutArguments;
                shortcut.WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))!;
                shortcut.Description = ShortcutDescription;
                shortcut.IconLocation = $"{shortcut.TargetPath},0";
                shortcut.Save();
            }
            finally
            {
                Marshal.ReleaseComObject(shortcut);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }

    /// <summary>Removes the site shortcut only when it still targets the given executable.</summary>
    [SupportedOSPlatform("windows")]
    public bool RemoveShortcutIfOwned(GuideShortcutSite site, string executablePath)
    {
        ThrowIfNotWindows();
        var path = ShortcutPath(site);
        if (!File.Exists(path))
        {
            return true;
        }

        string? target = null;
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(path);
            try
            {
                target = (string?)shortcut.TargetPath;
            }
            finally
            {
                Marshal.ReleaseComObject(shortcut);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }

        if (!string.Equals(target, Path.TrimEndingDirectorySeparator(Path.GetFullPath(executablePath)),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>Whether autostart is enabled for exactly this executable.</summary>
    [SupportedOSPlatform("windows")]
    public bool IsAutoStartEnabled(string executablePath)
    {
        ThrowIfNotWindows();
        using var key = OpenRunKey(writable: false);
        return string.Equals(
            key?.GetValue(AutoStartValueName) as string,
            AutoStartCommand(executablePath),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Writes the per-user logon autostart entry for this executable.</summary>
    [SupportedOSPlatform("windows")]
    public void EnableAutoStart(string executablePath)
    {
        ThrowIfNotWindows();
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        using var key = OpenRunKey(writable: true)
                        ?? throw new InvalidOperationException("The per-user Run registry key is not writable.");
        key.SetValue(AutoStartValueName, AutoStartCommand(executablePath), RegistryValueKind.String);
    }

    /// <summary>Removes the autostart entry only while it still names this executable.</summary>
    [SupportedOSPlatform("windows")]
    public bool DisableAutoStartIfOwned(string executablePath)
    {
        ThrowIfNotWindows();
        using var key = OpenRunKey(writable: true);
        if (key?.GetValue(AutoStartValueName) is not string recorded)
        {
            return true;
        }

        if (!string.Equals(recorded, AutoStartCommand(executablePath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        key.DeleteValue(AutoStartValueName, throwOnMissingValue: false);
        return true;
    }

    [SupportedOSPlatform("windows")]
    private RegistryKey? OpenRunKey(bool writable)
        => runKey ?? Registry.CurrentUser.OpenSubKey(RunSubKey, writable);
}
