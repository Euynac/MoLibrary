using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Runs file-based skill scripts as local subprocesses.
/// </summary>
/// <remarks>
/// This runner is intentionally small and mirrors Agent Framework's sample runner behavior. It maps common
/// script extensions to local interpreters, passes skill-script arguments as command-line flags, captures
/// standard output and standard error, and kills the process tree when the invocation is cancelled.
/// Hosts that need sandboxing, allow-lists, secrets isolation, or remote execution should provide their own
/// <see cref="AgentFileSkillScriptRunner"/> instead.
/// </remarks>
public static class MonicaSubprocessSkillScriptRunner
{
    private static readonly IReadOnlyDictionary<string, string> INTERPRETERS =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".py"] = "python3",
            [".js"] = "node",
            [".sh"] = "bash",
            [".ps1"] = "pwsh"
        };

    /// <summary>
    /// Executes the file-based skill script as a local subprocess.
    /// </summary>
    /// <param name="skill">Skill that owns the script.</param>
    /// <param name="script">Script to execute.</param>
    /// <param name="arguments">Arguments supplied by the model through Agent Framework.</param>
    /// <param name="cancellationToken">Cancellation token for the process invocation.</param>
    /// <returns>Captured script output or an error string suitable for returning to the agent loop.</returns>
    public static async Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(script);

        if (!File.Exists(script.FullPath))
        {
            return $"Error: Script file not found: {script.FullPath}";
        }

        var startInfo = CreateStartInfo(script);
        AddArguments(startInfo, arguments);

        Process? process = null;
        try
        {
            process = Process.Start(startInfo);
            if (process is null)
            {
                return $"Error: Failed to start process for script '{script.Name}'.";
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            return FormatResult(output, error, process.ExitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process?.Kill(entireProcessTree: true);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"Error: Failed to execute script '{script.Name}': {ex.Message}";
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static ProcessStartInfo CreateStartInfo(AgentFileSkillScript script)
    {
        var extension = Path.GetExtension(script.FullPath);
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(script.FullPath) ?? "."
        };

        if (INTERPRETERS.TryGetValue(extension, out var interpreter))
        {
            startInfo.FileName = interpreter;
            startInfo.ArgumentList.Add(script.FullPath);
            return startInfo;
        }

        startInfo.FileName = script.FullPath;
        return startInfo;
    }

    private static void AddArguments(ProcessStartInfo startInfo, AIFunctionArguments? arguments)
    {
        if (arguments is null)
        {
            return;
        }

        foreach (var (key, value) in arguments)
        {
            if (value is bool boolValue)
            {
                if (boolValue)
                {
                    startInfo.ArgumentList.Add(NormalizeKey(key));
                }

                continue;
            }

            if (value is null)
            {
                continue;
            }

            startInfo.ArgumentList.Add(NormalizeKey(key));
            startInfo.ArgumentList.Add(value.ToString()!);
        }
    }

    private static string FormatResult(string output, string error, int exitCode)
    {
        var result = output;
        if (!string.IsNullOrEmpty(error))
        {
            result += $"\nStderr:\n{error}";
        }

        if (exitCode != 0)
        {
            result += $"\nScript exited with code {exitCode}";
        }

        return string.IsNullOrEmpty(result) ? "(no output)" : result.Trim();
    }

    private static string NormalizeKey(string key)
    {
        return "--" + key.TrimStart('-');
    }
}
