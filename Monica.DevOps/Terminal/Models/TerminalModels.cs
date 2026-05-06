namespace Monica.DevOps.Terminal.Models;

/// <summary>
/// Runtime session state for the local terminal experience.
/// </summary>
public sealed class TerminalSession
{
    /// <summary>
    /// Gets or sets the stable session identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current working directory used by the next command.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    /// Gets the commands previously submitted to this session.
    /// </summary>
    public List<string> History { get; } = [];

    /// <summary>
    /// Gets the output lines retained for this session across UI session switches.
    /// </summary>
    public List<TerminalOutputLine> Output { get; } = [];

    /// <summary>
    /// Gets or sets the latest completed command result for this session.
    /// </summary>
    public TerminalCommandResult? LastResult { get; set; }

    /// <summary>
    /// Gets a value indicating whether a command is currently running.
    /// </summary>
    public bool IsRunning => ActiveCommand is not null;

    /// <summary>
    /// Records a submitted command and trims retained history to the configured limit.
    /// </summary>
    /// <param name="command">Submitted command text.</param>
    /// <param name="maxHistoryEntries">Maximum retained command history entries.</param>
    public void AddHistory(string command, int maxHistoryEntries)
    {
        if (maxHistoryEntries <= 0)
        {
            return;
        }

        History.Add(command);
        if (History.Count <= maxHistoryEntries)
        {
            return;
        }

        History.RemoveRange(0, History.Count - maxHistoryEntries);
    }

    /// <summary>
    /// Records a terminal output line and trims retained session output to the configured limit.
    /// </summary>
    /// <param name="line">Output line to append.</param>
    /// <param name="maxOutputLines">Maximum retained output lines.</param>
    public void AddOutput(TerminalOutputLine line, int maxOutputLines)
    {
        if (maxOutputLines <= 0)
        {
            return;
        }

        Output.Add(line);
        TrimOutput(maxOutputLines);
    }

    /// <summary>
    /// Clears retained output and latest result metadata for this session.
    /// </summary>
    public void ClearOutput()
    {
        Output.Clear();
        LastResult = null;
    }

    private void TrimOutput(int maxOutputLines)
    {
        if (Output.Count <= maxOutputLines)
        {
            return;
        }

        var removedCount = Output.Count - maxOutputLines + 1;
        Output.RemoveRange(0, removedCount);
        Output.Insert(0, new TerminalOutputLine
        {
            Stream = TerminalOutputStreams.System,
            Text = $"Session output truncated. Removed {removedCount} earlier line(s).",
            Timestamp = DateTimeOffset.Now
        });
    }

    internal TerminalRunningCommand? ActiveCommand { get; set; }
}

/// <summary>
/// Request used to create a terminal session.
/// </summary>
public sealed class TerminalSessionCreateRequest
{
    /// <summary>
    /// Gets or sets the optional display name. A generated name is used when omitted.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional initial working directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// Request used to execute a command in a terminal session.
/// </summary>
public sealed class TerminalCommandRequest
{
    /// <summary>
    /// Gets or sets the command text exactly as entered by the user.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command timeout in seconds. Defaults to the module option when omitted.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// A single output frame emitted by a terminal command.
/// </summary>
public sealed class TerminalOutputLine
{
    /// <summary>
    /// Gets or sets the stream that produced the output. Expected values are stdout, stderr, system, and input.
    /// </summary>
    public string Stream { get; set; } = TerminalOutputStreams.System;

    /// <summary>
    /// Gets or sets the line content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the line was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// Final command execution result.
/// </summary>
public sealed class TerminalCommandResult
{
    /// <summary>
    /// Gets or sets the executed command text.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory used to start the command.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process exit code.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets or sets whether the command was canceled before normal process exit.
    /// </summary>
    public bool Canceled { get; set; }

    /// <summary>
    /// Gets or sets whether the command timed out.
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Gets or sets the command duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the captured output lines.
    /// </summary>
    public List<TerminalOutputLine> Output { get; set; } = [];
}

/// <summary>
/// Well-known terminal output stream identifiers.
/// </summary>
public static class TerminalOutputStreams
{
    /// <summary>
    /// Standard output stream.
    /// </summary>
    public const string Stdout = "stdout";

    /// <summary>
    /// Standard error stream.
    /// </summary>
    public const string Stderr = "stderr";

    /// <summary>
    /// User-entered command stream.
    /// </summary>
    public const string Input = "input";

    /// <summary>
    /// Module-generated diagnostic stream.
    /// </summary>
    public const string System = "system";
}

internal sealed class TerminalRunningCommand(CancellationTokenSource cancellationTokenSource)
{
    public CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;
}
