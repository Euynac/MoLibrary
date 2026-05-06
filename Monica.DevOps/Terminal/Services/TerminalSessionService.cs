using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Monica.DevOps.Terminal.Models;
using Monica.DevOps.Terminal.Services.Support;
using Monica.Modules;

namespace Monica.DevOps.Terminal.Services;

/// <summary>
/// Owns local terminal sessions, command history, working directory state, and cancellation.
/// </summary>
public sealed class TerminalSessionService(
    TerminalCommandRunner commandRunner,
    TerminalEnvironmentDetector environmentDetector,
    IOptions<ModuleTerminalOption> options)
{
    private readonly ConcurrentDictionary<string, TerminalSessionOwnerState> _owners = [];
    private readonly ModuleTerminalOption _options = options.Value;

    private int MaxHistoryEntries => Math.Max(0, _options.MaxHistoryEntries);

    private int MaxOutputLines => Math.Max(0, _options.MaxOutputLines);

    /// <summary>
    /// Gets the detected terminal environment.
    /// </summary>
    /// <returns>The current environment information.</returns>
    public TerminalEnvironmentInfo GetEnvironment()
        => environmentDetector.Detect();

    /// <summary>
    /// Gets all active sessions owned by the requested browser or user partition.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <returns>Active terminal sessions for the owner.</returns>
    public IReadOnlyList<TerminalSession> GetSessions(string ownerKey)
        => GetOwnerState(ownerKey).GetSessions();

    /// <summary>
    /// Creates a new terminal session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="request">Session creation request.</param>
    /// <returns>The created session.</returns>
    public TerminalSession CreateSession(string ownerKey, TerminalSessionCreateRequest? request = null)
    {
        var ownerState = GetOwnerState(ownerKey);
        var now = DateTimeOffset.Now;
        var session = new TerminalSession
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ownerState.CreateSessionName(request?.Name),
            WorkingDirectory = NormalizeWorkingDirectory(request?.WorkingDirectory),
            CreatedAt = now,
            LastActivityAt = now
        };

        ownerState.Add(session);
        return session;
    }

    /// <summary>
    /// Gets an active session by identifier.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>The requested session.</returns>
    public TerminalSession GetSession(string ownerKey, string sessionId)
    {
        var session = GetOwnerState(ownerKey).Find(sessionId);
        if (session is not null)
        {
            return session;
        }

        throw new KeyNotFoundException($"Terminal session '{sessionId}' was not found.");
    }

    /// <summary>
    /// Closes a terminal session and cancels any active command.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    public void CloseSession(string ownerKey, string sessionId)
    {
        if (!GetOwnerState(ownerKey).TryRemove(sessionId, out var session) || session is null)
        {
            return;
        }

        session.ActiveCommand?.CancellationTokenSource.Cancel();
        session.ActiveCommand = null;
    }

    /// <summary>
    /// Cancels the command currently running in a session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    public void CancelCommand(string ownerKey, string sessionId)
    {
        var session = GetSession(ownerKey, sessionId);
        session.ActiveCommand?.CancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Clears retained output for the requested session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    public void ClearOutput(string ownerKey, string sessionId)
    {
        var session = GetSession(ownerKey, sessionId);
        lock (session)
        {
            session.ClearOutput();
            session.LastActivityAt = DateTimeOffset.Now;
        }
    }

    /// <summary>
    /// Executes a command in the requested session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="request">Command request.</param>
    /// <param name="outputCallback">Optional callback invoked when a command output line is captured.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command result.</returns>
    public async Task<TerminalCommandResult> ExecuteCommandAsync(
        string ownerKey,
        string sessionId,
        TerminalCommandRequest request,
        Func<TerminalOutputLine, CancellationToken, ValueTask>? outputCallback = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = GetSession(ownerKey, sessionId);
        var command = request.Command.Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command text is required.", nameof(request));
        }

        lock (session)
        {
            if (session.ActiveCommand is not null)
            {
                throw new InvalidOperationException("A command is already running in this terminal session.");
            }
        }

        session.AddHistory(command, MaxHistoryEntries);
        var inputLine = new TerminalOutputLine
        {
            Stream = TerminalOutputStreams.Input,
            Text = command,
            Timestamp = DateTimeOffset.Now
        };
        lock (session)
        {
            session.AddOutput(inputLine, MaxOutputLines);
        }
        if (outputCallback is not null)
        {
            await outputCallback(inputLine, ct);
        }

        if (TryApplyChangeDirectory(session, command, out var cdResult))
        {
            AppendResultOutput(session, cdResult);
            session.LastResult = cdResult;
            return cdResult;
        }

        using var commandCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (session)
        {
            session.ActiveCommand = new TerminalRunningCommand(commandCts);
        }

        try
        {
            var result = await commandRunner.ExecuteAsync(
                command,
                session.WorkingDirectory,
                request.TimeoutSeconds,
                CreateSessionOutputCallback(session, outputCallback),
                commandCts.Token);
            session.LastActivityAt = DateTimeOffset.Now;
            session.LastResult = result;
            return result;
        }
        finally
        {
            lock (session)
            {
                session.ActiveCommand = null;
            }
        }
    }

    private string NormalizeWorkingDirectory(string? workingDirectory)
    {
        var fallback = environmentDetector.Detect().DefaultWorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return fallback;
        }

        try
        {
            var normalized = Path.GetFullPath(workingDirectory.Trim().Trim('"'));
            return Directory.Exists(normalized) ? normalized : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private bool TryApplyChangeDirectory(TerminalSession session, string command, out TerminalCommandResult result)
    {
        result = new TerminalCommandResult
        {
            Command = command,
            WorkingDirectory = session.WorkingDirectory
        };

        var target = ParseChangeDirectoryTarget(command);
        if (target is null)
        {
            return false;
        }

        var nextDirectory = ResolveChangeDirectory(session.WorkingDirectory, target);
        if (!Directory.Exists(nextDirectory))
        {
            result.ExitCode = 1;
            result.Output.Add(new TerminalOutputLine
            {
                Stream = TerminalOutputStreams.Stderr,
                Text = $"Directory not found: {nextDirectory}",
                Timestamp = DateTimeOffset.Now
            });
            return true;
        }

        session.WorkingDirectory = nextDirectory;
        session.LastActivityAt = DateTimeOffset.Now;
        result.WorkingDirectory = nextDirectory;
        result.Output.Add(new TerminalOutputLine
        {
            Stream = TerminalOutputStreams.System,
            Text = $"Working directory changed to {nextDirectory}",
            Timestamp = DateTimeOffset.Now
        });
        return true;
    }

    private static string? ParseChangeDirectoryTarget(string command)
    {
        if (string.Equals(command, "cd", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (!command.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return command[3..].Trim().Trim('"', '\'');
    }

    private static string ResolveChangeDirectory(string currentDirectory, string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var expandedTarget = target.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), target[2..])
            : target;

        return Path.GetFullPath(Path.IsPathRooted(expandedTarget)
            ? expandedTarget
            : Path.Combine(currentDirectory, expandedTarget));
    }

    private TerminalSessionOwnerState GetOwnerState(string ownerKey)
    {
        var normalizedOwnerKey = NormalizeOwnerKey(ownerKey);
        return _owners.GetOrAdd(normalizedOwnerKey, static _ => new TerminalSessionOwnerState());
    }

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey);
        return ownerKey.Trim();
    }

    private Func<TerminalOutputLine, CancellationToken, ValueTask> CreateSessionOutputCallback(
        TerminalSession session,
        Func<TerminalOutputLine, CancellationToken, ValueTask>? outputCallback)
    {
        return async (line, cancellationToken) =>
        {
            lock (session)
            {
                session.AddOutput(line, MaxOutputLines);
            }

            if (outputCallback is not null)
            {
                await outputCallback(line, cancellationToken);
            }
        };
    }

    private void AppendResultOutput(TerminalSession session, TerminalCommandResult result)
    {
        lock (session)
        {
            foreach (var line in result.Output)
            {
                session.AddOutput(line, MaxOutputLines);
            }
        }
    }

    private sealed class TerminalSessionOwnerState
    {
        private readonly ConcurrentDictionary<string, TerminalSession> _sessions = [];
        private int _createdSessionCount;

        public IReadOnlyList<TerminalSession> GetSessions()
            => _sessions.Values
                .OrderByDescending(static session => session.LastActivityAt)
                .ToList();

        public string CreateSessionName(string? requestedName)
        {
            if (!string.IsNullOrWhiteSpace(requestedName))
            {
                return requestedName.Trim();
            }

            var sequence = Interlocked.Increment(ref _createdSessionCount);
            return $"Terminal {sequence}";
        }

        public void Add(TerminalSession session)
            => _sessions[session.Id] = session;

        public TerminalSession? Find(string sessionId)
            => _sessions.TryGetValue(sessionId, out var session) ? session : null;

        public bool TryRemove(string sessionId, out TerminalSession? session)
            => _sessions.TryRemove(sessionId, out session);
    }
}
