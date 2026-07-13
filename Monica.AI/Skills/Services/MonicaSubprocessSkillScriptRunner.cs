using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Runs file-based skill scripts as local subprocesses.
/// </summary>
/// <remarks>
/// This runner is intentionally small and mirrors Agent Framework's sample runner behavior. It maps common
/// script extensions to local interpreters, passes the Agent Framework string-array arguments unchanged, captures
/// standard output and standard error, and kills the process tree when the invocation is cancelled.
/// Hosts that need sandboxing, allow-lists, secrets isolation, or remote execution should provide their own
/// <see cref="AgentFileSkillScriptRunner"/> instead.
/// </remarks>
internal static class MonicaSubprocessSkillScriptRunner
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
    /// <param name="serviceProvider">Optional invocation service provider.</param>
    /// <param name="cancellationToken">Cancellation token for the process invocation.</param>
    /// <returns>Captured script output or an error string suitable for returning to the agent loop.</returns>
    internal static async Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        JsonElement? arguments,
        IServiceProvider? serviceProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(script);
        _ = serviceProvider;

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

    private static void AddArguments(ProcessStartInfo startInfo, JsonElement? arguments)
    {
        if (arguments is null
            || arguments.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        if (arguments.Value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"File skill script arguments must be a JSON string array but received '{arguments.Value.ValueKind}'.");
        }

        foreach (var argument in arguments.Value.EnumerateArray())
        {
            startInfo.ArgumentList.Add(argument.GetString()
                                       ?? throw new InvalidOperationException(
                                           "File skill script arguments must contain only strings."));
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
}
