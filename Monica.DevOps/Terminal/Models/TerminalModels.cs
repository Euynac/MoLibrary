namespace Monica.DevOps.Terminal.Models;

/// <summary>
/// Runtime session state for the local terminal experience.
/// </summary>
public sealed class TerminalSession
{
    private readonly object _stateLock = new();
    private readonly List<string> _history = [];
    private readonly List<TerminalOutputLine> _output = [];
    private TerminalCommandResult? _lastResult;
    private TerminalRunningCommand? _activeCommand;

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
    public IReadOnlyList<string> History => GetHistorySnapshot();

    /// <summary>
    /// Gets the output lines retained for this session across UI session switches.
    /// </summary>
    public IReadOnlyList<TerminalOutputLine> Output => GetOutputSnapshot();

    /// <summary>
    /// Gets or sets the latest completed command result for this session.
    /// </summary>
    public TerminalCommandResult? LastResult
    {
        get
        {
            lock (_stateLock)
            {
                return _lastResult;
            }
        }
        set
        {
            lock (_stateLock)
            {
                _lastResult = value;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a command is currently running.
    /// </summary>
    public bool IsRunning => ActiveCommand is not null;

    /// <summary>
    /// Attempts to mark a command as active for this session.
    /// </summary>
    /// <param name="runningCommand">Running command state to attach.</param>
    /// <returns><see langword="true"/> when the command was attached; otherwise another command is already running.</returns>
    internal bool TryBeginCommand(TerminalRunningCommand runningCommand)
    {
        lock (_stateLock)
        {
            if (_activeCommand is not null)
            {
                return false;
            }

            _activeCommand = runningCommand;
            return true;
        }
    }

    /// <summary>
    /// Clears the active command when it matches the supplied command instance.
    /// </summary>
    /// <param name="runningCommand">Running command instance to clear.</param>
    internal void EndCommand(TerminalRunningCommand runningCommand)
    {
        lock (_stateLock)
        {
            if (ReferenceEquals(_activeCommand, runningCommand))
            {
                _activeCommand = null;
            }
        }
    }

    /// <summary>
    /// Cancels the active command when one is running.
    /// </summary>
    internal void CancelActiveCommand()
    {
        ActiveCommand?.CancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Records a submitted command and trims retained history to the configured limit.
    /// </summary>
    /// <param name="command">Submitted command text.</param>
    /// <param name="maxHistoryEntries">Maximum retained command history entries.</param>
    public void AddHistory(string command, int maxHistoryEntries)
    {
        lock (_stateLock)
        {
            if (maxHistoryEntries <= 0)
            {
                return;
            }

            _history.Add(command);
            if (_history.Count <= maxHistoryEntries)
            {
                return;
            }

            _history.RemoveRange(0, _history.Count - maxHistoryEntries);
        }
    }

    /// <summary>
    /// Records a terminal output line and trims retained session output to the configured limit.
    /// </summary>
    /// <param name="line">Output line to append.</param>
    /// <param name="maxOutputLines">Maximum retained output lines.</param>
    public void AddOutput(TerminalOutputLine line, int maxOutputLines)
    {
        lock (_stateLock)
        {
            if (maxOutputLines <= 0)
            {
                return;
            }

            _output.Add(line);
            TrimOutput(maxOutputLines);
        }
    }

    /// <summary>
    /// Clears retained output and latest result metadata for this session.
    /// </summary>
    public void ClearOutput()
    {
        lock (_stateLock)
        {
            _output.Clear();
            _lastResult = null;
        }
    }

    /// <summary>
    /// Creates a stable copy of retained command history for rendering or external enumeration.
    /// </summary>
    /// <returns>A snapshot of retained command history.</returns>
    public IReadOnlyList<string> GetHistorySnapshot()
    {
        lock (_stateLock)
        {
            return [.._history];
        }
    }

    /// <summary>
    /// Creates a stable copy of retained output for rendering or external enumeration.
    /// </summary>
    /// <returns>A snapshot of retained output lines.</returns>
    public IReadOnlyList<TerminalOutputLine> GetOutputSnapshot()
    {
        lock (_stateLock)
        {
            return [.._output];
        }
    }

    private void TrimOutput(int maxOutputLines)
    {
        if (_output.Count <= maxOutputLines)
        {
            return;
        }

        var removedCount = _output.Count - maxOutputLines + 1;
        _output.RemoveRange(0, removedCount);
        _output.Insert(0, new TerminalOutputLine
        {
            Stream = TerminalOutputStreams.System,
            Text = $"Session output truncated. Removed {removedCount} earlier line(s).",
            Timestamp = DateTimeOffset.Now
        });
    }

    private TerminalRunningCommand? ActiveCommand
    {
        get
        {
            lock (_stateLock)
            {
                return _activeCommand;
            }
        }
        set
        {
            lock (_stateLock)
            {
                _activeCommand = value;
            }
        }
    }
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
