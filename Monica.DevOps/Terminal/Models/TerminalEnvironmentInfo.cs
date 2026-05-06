using System.Runtime.InteropServices;

namespace Monica.DevOps.Terminal.Models;

/// <summary>
/// Describes the local environment used by the terminal module to select shell behavior.
/// </summary>
public sealed class TerminalEnvironmentInfo
{
    /// <summary>
    /// Gets or sets the broad operating system family, such as Windows, Linux, macOS, or Unknown.
    /// </summary>
    public string OperatingSystemFamily { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system description reported by the .NET runtime.
    /// </summary>
    public string OperatingSystemDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system architecture.
    /// </summary>
    public Architecture Architecture { get; set; }

    /// <summary>
    /// Gets or sets the runtime identifier reported by the .NET runtime.
    /// </summary>
    public string RuntimeIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the shell executable used for command execution.
    /// </summary>
    public string ShellPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the shell arguments passed before the user command.
    /// </summary>
    public List<string> ShellArguments { get; set; } = [];

    /// <summary>
    /// Gets or sets the process working directory used when a session does not specify one.
    /// </summary>
    public string DefaultWorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application base directory.
    /// </summary>
    public string ApplicationBaseDirectory { get; set; } = string.Empty;
}
