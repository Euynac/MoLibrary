using System.Diagnostics;

namespace Monica.Framework.UI.UISystemInfo.Models;

/// <summary>
/// System information response model
/// </summary>
public class SystemInfoResponse
{
    /// <summary>
    /// Build time
    /// </summary>
    public DateTime BuildTime { get; set; }

    /// <summary>
    /// local time
    /// </summary>
    public DateTime LocalTime { get; set; }

    /// <summary>
    /// UTC time
    /// </summary>
    public DateTime UtcTime { get; set; }

    /// <summary>
    /// Product version (simplified mode)
    /// </summary>
    public string? ProductVersion { get; set; }

    /// <summary>
    /// process start time
    /// </summary>
    public DateTime? ProcessStartTime { get; set; }

    /// <summary>
    /// File information (verbose mode)
    /// </summary>
    public FileVersionInfo? FileInfo { get; set; }

    /// <summary>
    /// Environmental information (verbose mode)
    /// </summary>
    public EnvironmentInfo? EnvironmentInfo { get; set; }
}

/// <summary>
/// environmental information
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// .NET version
    /// </summary>
    public Version? Version { get; set; }

    /// <summary>
    /// username
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Machine name
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Operating system version
    /// </summary>
    public OperatingSystem? OSVersion { get; set; }

    /// <summary>
    /// Process ID
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// process start time
    /// </summary>
    public DateTime? ProcessStartTime { get; set; }

    /// <summary>
    /// current directory
    /// </summary>
    public string? CurrentDirectory { get; set; }

    /// <summary>
    /// Has the shutdown started?
    /// </summary>
    public bool HasShutdownStarted { get; set; }

    /// <summary>
    /// Is it a 64-bit operating system?
    /// </summary>
    public bool Is64BitOperatingSystem { get; set; }

    /// <summary>
    /// Is it a 64-bit process?
    /// </summary>
    public bool Is64BitProcess { get; set; }

    /// <summary>
    /// Is the process a privileged process?
    /// </summary>
    public bool IsPrivilegedProcess { get; set; }

    /// <summary>
    /// System tick count
    /// </summary>
    public int TickCount { get; set; }

    /// <summary>
    /// User domain name
    /// </summary>
    public string? UserDomainName { get; set; }

    /// <summary>
    /// working set size
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// System page size
    /// </summary>
    public int SystemPageSize { get; set; }

    /// <summary>
    /// environment variables
    /// </summary>
    public IDictionary<string, object?>? Environments { get; set; }

    /// <summary>
    /// Is the user interactive?
    /// </summary>
    public bool UserInteractive { get; set; }

    /// <summary>
    /// process path
    /// </summary>
    public string? ProcessPath { get; set; }
} 