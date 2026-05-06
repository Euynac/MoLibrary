using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.DevOps.Terminal.Models;
using Monica.DevOps.Terminal.Services.Support;
using Monica.Modules;

namespace Monica.DevOps.Terminal.Services;

/// <summary>
/// Executes local commands through the detected platform shell.
/// </summary>
public sealed class TerminalCommandRunner(
    TerminalEnvironmentDetector environmentDetector,
    IOptions<ModuleTerminalOption> options,
    ILogger<TerminalCommandRunner> logger)
{
    private readonly ModuleTerminalOption _options = options.Value;

    /// <summary>
    /// Executes a command and captures stdout/stderr output.
    /// </summary>
    /// <param name="command">Command text to execute.</param>
    /// <param name="workingDirectory">Working directory used for the process.</param>
    /// <param name="timeoutSeconds">Optional timeout override in seconds.</param>
    /// <param name="outputCallback">Optional callback invoked when a stdout, stderr, or system output line is captured.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The captured command result.</returns>
    public async Task<TerminalCommandResult> ExecuteAsync(
        string command,
        string workingDirectory,
        int? timeoutSeconds,
        Func<TerminalOutputLine, CancellationToken, ValueTask>? outputCallback = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var environment = environmentDetector.Detect();
        var normalizedTimeoutSeconds = Math.Clamp(
            timeoutSeconds ?? _options.DefaultTimeoutSeconds,
            1,
            _options.MaxTimeoutSeconds);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(normalizedTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var result = new TerminalCommandResult
        {
            Command = command,
            WorkingDirectory = workingDirectory
        };

        var startedAt = Stopwatch.GetTimestamp();
        using var process = CreateProcess(environment, command, workingDirectory);
        process.OutputDataReceived += (_, args) => AddOutputLine(result, TerminalOutputStreams.Stdout, args.Data, outputCallback, ct);
        process.ErrorDataReceived += (_, args) => AddOutputLine(result, TerminalOutputStreams.Stderr, args.Data, outputCallback, ct);

        try
        {
            logger.LogInformation("Executing terminal command in {WorkingDirectory}: {Command}", workingDirectory, command);
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start the terminal command process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(linkedCts.Token);
            result.ExitCode = process.ExitCode;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            result.TimedOut = true;
            result.Canceled = true;
            result.ExitCode = -1;
            KillProcessTree(process);
            AddOutputLine(
                result,
                TerminalOutputStreams.System,
                $"Command timed out after {normalizedTimeoutSeconds} second(s).",
                outputCallback,
                CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            result.Canceled = true;
            result.ExitCode = -1;
            KillProcessTree(process);
            AddOutputLine(result, TerminalOutputStreams.System, "Command canceled.", outputCallback, CancellationToken.None);
        }
        finally
        {
            result.Duration = Stopwatch.GetElapsedTime(startedAt);
            TrimOutput(result.Output);
        }

        return result;
    }

    private static Process CreateProcess(TerminalEnvironmentInfo environment, string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = environment.ShellPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in environment.ShellArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(command);

        return new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
    }

    private static void AddOutputLine(
        TerminalCommandResult result,
        string stream,
        string? text,
        Func<TerminalOutputLine, CancellationToken, ValueTask>? outputCallback,
        CancellationToken ct)
    {
        if (text is null)
        {
            return;
        }

        var line = new TerminalOutputLine
        {
            Stream = stream,
            Text = text,
            Timestamp = DateTimeOffset.Now
        };

        lock (result.Output)
        {
            result.Output.Add(line);
        }

        if (outputCallback is not null)
        {
            _ = outputCallback(line, ct);
        }
    }

    private void TrimOutput(List<TerminalOutputLine> output)
    {
        var maxOutputLines = Math.Max(0, _options.MaxOutputLines);
        if (maxOutputLines <= 0)
        {
            output.Clear();
            return;
        }

        if (output.Count <= maxOutputLines)
        {
            return;
        }

        var removedCount = output.Count - maxOutputLines + 1;
        output.RemoveRange(0, removedCount);
        output.Insert(0, new TerminalOutputLine
        {
            Stream = TerminalOutputStreams.System,
            Text = $"Output truncated. Removed {removedCount} earlier line(s).",
            Timestamp = DateTimeOffset.Now
        });
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
