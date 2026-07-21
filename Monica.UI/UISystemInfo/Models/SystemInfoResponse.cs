using System.Diagnostics;

namespace Monica.UI.UISystemInfo.Models;

/// <summary>
/// Represents the data returned by the system information endpoint.
/// </summary>
public class SystemInfoResponse
{
    /// <summary>
    /// Gets or sets the application build timestamp.
    /// </summary>
    public DateTime BuildTime { get; set; }

    /// <summary>
    /// Gets or sets the current local server time.
    /// </summary>
    public DateTime LocalTime { get; set; }

    /// <summary>
    /// Gets or sets the current UTC server time.
    /// </summary>
    public DateTime UtcTime { get; set; }

    /// <summary>
    /// Gets or sets the server time zone and the UTC offset in effect when this response was captured.
    /// </summary>
    public required SystemTimeZone TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the product version returned in simplified mode.
    /// </summary>
    public string? ProductVersion { get; set; }

    /// <summary>
    /// Gets or sets the process start time when available.
    /// </summary>
    public DateTime? ProcessStartTime { get; set; }

    /// <summary>
    /// Gets or sets the addresses the current ASP.NET Core server is listening on.
    /// </summary>
    public IReadOnlyList<string> ListeningAddresses { get; set; } = [];

    /// <summary>
    /// Gets or sets verbose file metadata.
    /// </summary>
    public FileVersionInfo? FileInfo { get; set; }

    /// <summary>
    /// Gets or sets verbose environment details.
    /// </summary>
    public EnvironmentInfo? EnvironmentInfo { get; set; }
}

/// <summary>
/// Represents verbose host environment details for the system information page.
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// Gets or sets the .NET runtime version.
    /// </summary>
    public Version? Version { get; set; }

    /// <summary>
    /// Gets or sets the current user name.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the machine name.
    /// </summary>
    public string? MachineName { get; set; }

    /// <summary>
    /// Gets or sets the operating system version.
    /// </summary>
    public OperatingSystem? OSVersion { get; set; }

    /// <summary>
    /// Gets or sets the current process identifier.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the current process start time.
    /// </summary>
    public DateTime? ProcessStartTime { get; set; }

    /// <summary>
    /// Gets or sets the current working directory.
    /// </summary>
    public string? CurrentDirectory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether process shutdown has started.
    /// </summary>
    public bool HasShutdownStarted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operating system is 64-bit.
    /// </summary>
    public bool Is64BitOperatingSystem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the process is 64-bit.
    /// </summary>
    public bool Is64BitProcess { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the process is privileged.
    /// </summary>
    public bool IsPrivilegedProcess { get; set; }

    /// <summary>
    /// Gets or sets the current system tick count.
    /// </summary>
    public int TickCount { get; set; }

    /// <summary>
    /// Gets or sets the current user domain name.
    /// </summary>
    public string? UserDomainName { get; set; }

    /// <summary>
    /// Gets or sets the working set size in bytes.
    /// </summary>
    public long WorkingSet { get; set; }

    /// <summary>
    /// Gets or sets the operating system page size in bytes.
    /// </summary>
    public int SystemPageSize { get; set; }

    /// <summary>
    /// Gets or sets the captured environment variables.
    /// </summary>
    public IDictionary<string, object?>? Environments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current process is running interactively.
    /// </summary>
    public bool UserInteractive { get; set; }

    /// <summary>
    /// Gets or sets the current process path.
    /// </summary>
    public string? ProcessPath { get; set; }
}
