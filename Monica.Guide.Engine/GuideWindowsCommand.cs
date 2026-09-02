using System.Text;

namespace Monica.Guide;

internal static class GuideWindowsCommand
{
    private const string DefaultPathExtensions = ".COM;.EXE;.BAT;.CMD";
    private static readonly char[] InvalidCommandCharacters = ['\0', '\r', '\n', '"', '*', '?'];
    private static readonly char[] UnsafeBatchCharacters = ['\0', '\r', '\n', '"', '%', '!', '^', '&', '|', '<', '>'];

    internal static string? Resolve(
        string command,
        Func<string, GuideProcessResult> runWhere,
        string? path,
        string? pathExtensions,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(command)
            || command.IndexOfAny(InvalidCommandCharacters) >= 0)
        {
            return null;
        }

        var normalizedCommand = command.Trim();
        if (ContainsDirectorySeparator(normalizedCommand) || Path.IsPathRooted(normalizedCommand))
        {
            return ResolveExplicitPath(normalizedCommand, pathExtensions, fileExists);
        }

        var whereResult = runWhere(normalizedCommand);
        if (whereResult.ExitCode == 0)
        {
            var executableExtensions = ParsePathExtensions(pathExtensions);
            string? extensionlessFallback = null;
            foreach (var line in whereResult.StandardOutput.Split(
                         ['\r', '\n'],
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var candidate = TrimMatchingQuotes(line);
                if (!fileExists(candidate))
                {
                    continue;
                }

                var extension = GetWindowsExtension(candidate);
                if (extension.Length == 0)
                {
                    // npm global installs list an extensionless POSIX shim ahead of the
                    // launchable .cmd sibling; keep it only as a last resort for genuine
                    // extensionless PE binaries.
                    extensionlessFallback ??= candidate;
                    continue;
                }

                if (executableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            if (extensionlessFallback is not null)
            {
                return extensionlessFallback;
            }
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var extensions = GetCandidateExtensions(normalizedCommand, pathExtensions);
        foreach (var rawDirectory in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var directory = TrimMatchingQuotes(rawDirectory);
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, normalizedCommand + extension);
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    internal static bool IsBatchFile(string path)
    {
        var extension = GetWindowsExtension(path);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryBuildBatchCommandLine(
        string executable,
        IReadOnlyList<string> arguments,
        out string commandLine,
        out string? error)
    {
        if (!IsSafeBatchToken(executable))
        {
            commandLine = string.Empty;
            error = "A Windows batch command token contains characters that cannot be passed safely.";
            return false;
        }

        var builder = new StringBuilder();
        builder.Append('"');
        if (!TryAppendBatchToken(builder, executable, out error))
        {
            commandLine = string.Empty;
            return false;
        }
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            if (!TryAppendBatchToken(builder, argument, out error))
            {
                commandLine = string.Empty;
                return false;
            }
        }
        builder.Append('"');

        commandLine = builder.ToString();
        error = null;
        return true;
    }

    private static string? ResolveExplicitPath(
        string command,
        string? pathExtensions,
        Func<string, bool> fileExists)
    {
        foreach (var extension in GetCandidateExtensions(command, pathExtensions))
        {
            var candidate = command + extension;
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        // Extensionless PE binaries remain launchable when no PATHEXT sibling exists.
        return !HasWindowsExtension(command) && fileExists(command) ? command : null;
    }

    private static IReadOnlyList<string> GetCandidateExtensions(string command, string? pathExtensions)
        => HasWindowsExtension(command) ? [string.Empty] : ParsePathExtensions(pathExtensions);

    /// <summary>
    /// Windows path semantics regardless of the host OS: where.exe output and PATHEXT
    /// candidates must be split on '\'/'/' separators, never on the host's, so a POSIX
    /// host still reads <c>C:\u\.local\bin\claude</c> as extensionless.
    /// </summary>
    private static string GetWindowsExtension(string path)
    {
        var fileName = path[(Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/')) + 1)..];
        var dot = fileName.LastIndexOf('.');
        return dot < 0 ? string.Empty : fileName[dot..];
    }

    private static bool HasWindowsExtension(string path)
        => GetWindowsExtension(path).Length > 0;

    private static IReadOnlyList<string> ParsePathExtensions(string? pathExtensions)
    {
        var configured = string.IsNullOrWhiteSpace(pathExtensions)
            ? DefaultPathExtensions
            : pathExtensions;
        var extensions = configured
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static extension => extension.Length > 1
                                       && extension[0] == '.'
                                       && extension[1..].All(char.IsAsciiLetterOrDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return extensions.Length == 0
            ? DefaultPathExtensions.Split(';')
            : extensions;
    }

    private static bool TryAppendBatchToken(StringBuilder builder, string value, out string? error)
    {
        if (!IsSafeBatchToken(value))
        {
            error = "A Windows batch command token contains characters that cannot be passed safely.";
            return false;
        }

        builder.Append('"');
        builder.Append(value);
        builder.Append('"');
        error = null;
        return true;
    }

    private static bool IsSafeBatchToken(string value)
        => value.IndexOfAny(UnsafeBatchCharacters) < 0;

    private static bool ContainsDirectorySeparator(string value)
        => value.Contains('\\') || value.Contains('/');

    private static string TrimMatchingQuotes(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
}
