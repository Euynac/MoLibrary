using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Monica.AI.Abstractions;
using Monica.AI.Skills.Models;
using Monica.Tool.Runtime;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Performs bounded, read-only filesystem inspection for Monica agent skills.
/// </summary>
internal sealed class ReadOnlyFileAccessService(
    ReadOnlyFileAccessOptions options,
    ITokenCountProvider tokenCountProvider,
    ILogger<ReadOnlyFileAccessService> logger)
{
    private readonly IReadOnlyList<ReadOnlyFileRoot> _roots = BuildRoots(options.Roots);

    /// <summary>
    /// Gets whether at least one configured root is available to the skill.
    /// </summary>
    public bool HasRoots => _roots.Count > 0;

    /// <summary>
    /// Lists all configured roots.
    /// </summary>
    public ReadOnlyFileRootListResult ListRoots()
    {
        var roots = _roots
            .Select(static root => new ReadOnlyFileRootInfo(
                root.Name,
                root.Description,
                root.FullPath,
                Directory.Exists(root.FullPath)))
            .ToList();

        return new ReadOnlyFileRootListResult(roots, roots.Count);
    }

    /// <summary>
    /// Reads a bounded line window from a text file.
    /// </summary>
    public async Task<ReadOnlyFileReadResult> ReadFileAsync(
        string rootName,
        string path,
        int startLine,
        int maxLines,
        int? maxTokens,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = GetRoot(rootName);
        var targetPath = ResolvePath(root, path, requireFilePath: true);
        var displayPath = ToDisplayPath(root, targetPath);
        if (!File.Exists(targetPath))
        {
            return new ReadOnlyFileReadResult
            {
                RootName = root.Name,
                Path = displayPath,
                FullPath = targetPath,
                Exists = false,
                IsText = false,
                StartLine = Math.Max(1, startLine),
                ReturnedLineCount = 0,
                HasMore = false,
                TokenTruncated = false,
                Message = "The requested file does not exist inside the configured root."
            };
        }

        if (await LooksBinaryAsync(targetPath, ct).ConfigureAwait(false))
        {
            return new ReadOnlyFileReadResult
            {
                RootName = root.Name,
                Path = displayPath,
                FullPath = targetPath,
                Exists = true,
                IsText = false,
                StartLine = Math.Max(1, startLine),
                ReturnedLineCount = 0,
                HasMore = false,
                TokenTruncated = false,
                Message = "The requested file appears to be binary and is not supported by this read-only text tool."
            };
        }

        var effectiveStartLine = Math.Max(1, startLine);
        var effectiveMaxLines = Clamp(maxLines, 1, options.MaxReadLines, options.DefaultReadLines);
        var effectiveMaxTokens = Clamp(maxTokens ?? options.DefaultReadTokens, 1, options.MaxReadTokens, options.DefaultReadTokens);
        var selectedLines = new List<string>(effectiveMaxLines);
        var totalLineCount = 0;
        var hasMore = false;

        await foreach (var line in File.ReadLinesAsync(targetPath, ct).ConfigureAwait(false))
        {
            totalLineCount++;
            if (totalLineCount < effectiveStartLine)
            {
                continue;
            }

            if (selectedLines.Count >= effectiveMaxLines)
            {
                hasMore = true;
                break;
            }

            selectedLines.Add(line);
        }

        var selectedText = string.Join(Environment.NewLine, selectedLines);
        var truncation = tokenCountProvider.TruncateToMaxTokens(selectedText, effectiveMaxTokens);
        var nextStartLine = hasMore
            ? effectiveStartLine + selectedLines.Count
            : (int?)null;

        return new ReadOnlyFileReadResult
        {
            RootName = root.Name,
            Path = displayPath,
            FullPath = targetPath,
            Exists = true,
            IsText = true,
            StartLine = effectiveStartLine,
            ReturnedLineCount = selectedLines.Count,
            TotalLineCount = hasMore ? null : totalLineCount,
            Content = truncation.Text,
            NextStartLine = nextStartLine,
            HasMore = hasMore,
            TokenTruncated = truncation.WasTruncated,
            Message = BuildReadMessage(nextStartLine, truncation.WasTruncated)
        };
    }

    /// <summary>
    /// Searches file content with ripgrep JSON output.
    /// </summary>
    public async Task<ReadOnlyFileSearchResult> SearchFilesAsync(
        string rootName,
        string pattern,
        string? directoryPath,
        string? glob,
        bool ignoreCase,
        int contextLines,
        int offset,
        int maxResults,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var root = GetRoot(rootName);
        var searchPath = ResolvePath(root, directoryPath, requireFilePath: false);
        if (!Directory.Exists(searchPath))
        {
            throw new DirectoryNotFoundException($"Search directory '{ToDisplayPath(root, searchPath)}' does not exist inside root '{root.Name}'.");
        }

        var effectiveContextLines = Clamp(contextLines, 0, options.MaxSearchContextLines, 0);
        var effectiveOffset = Math.Max(0, offset);
        var effectiveMaxResults = Clamp(maxResults, 1, options.MaxResults, options.DefaultResults);
        var maxSearchLineCharacters = Math.Max(1, options.MaxSearchLineCharacters);
        var args = new List<string>
        {
            "--json",
            "--hidden",
            "--max-columns",
            maxSearchLineCharacters.ToString(),
            "--max-columns-preview",
            "--glob",
            "!.git/**",
            "--glob",
            "!.hg/**",
            "--glob",
            "!.svn/**"
        };

        if (ignoreCase)
        {
            args.Add("-i");
        }

        if (effectiveContextLines > 0)
        {
            args.Add("-C");
            args.Add(effectiveContextLines.ToString());
        }

        if (!string.IsNullOrWhiteSpace(glob))
        {
            args.Add("--glob");
            args.Add(glob.Trim());
        }

        args.Add(pattern);
        args.Add(searchPath);

        var output = await RunRipgrepAsync(args, treatExitCodeOneAsSuccess: true, ct).ConfigureAwait(false);
        var parsed = ParseRipgrepSearch(output, root, effectiveOffset, effectiveMaxResults, maxSearchLineCharacters);
        var nextOffset = parsed.Truncated ? effectiveOffset + parsed.Results.Count : (int?)null;
        return new ReadOnlyFileSearchResult(
            root.Name,
            pattern,
            ToDisplayPath(root, searchPath),
            string.IsNullOrWhiteSpace(glob) ? null : glob.Trim(),
            ignoreCase,
            effectiveContextLines,
            effectiveOffset,
            effectiveMaxResults,
            parsed.Results,
            parsed.Results.Count,
            parsed.Truncated,
            parsed.Truncated,
            nextOffset,
            parsed.Truncated ? "More matches may exist. Increase offset to continue from the next result window." : null);
    }

    /// <summary>
    /// Lists files with ripgrep's file discovery mode.
    /// </summary>
    public async Task<ReadOnlyFileListResult> ListFilesAsync(
        string rootName,
        string? directoryPath,
        string? glob,
        int offset,
        int maxResults,
        CancellationToken ct = default)
    {
        var root = GetRoot(rootName);
        var listPath = ResolvePath(root, directoryPath, requireFilePath: false);
        if (!Directory.Exists(listPath))
        {
            throw new DirectoryNotFoundException($"List directory '{ToDisplayPath(root, listPath)}' does not exist inside root '{root.Name}'.");
        }

        var effectiveOffset = Math.Max(0, offset);
        var effectiveMaxResults = Clamp(maxResults, 1, options.MaxResults, options.DefaultResults);
        var args = new List<string>
        {
            "--files",
            "--hidden",
            "--glob",
            "!.git/**",
            "--glob",
            "!.hg/**",
            "--glob",
            "!.svn/**"
        };

        if (!string.IsNullOrWhiteSpace(glob))
        {
            args.Add("--glob");
            args.Add(glob.Trim());
        }

        args.Add(listPath);

        var output = await RunRipgrepAsync(args, treatExitCodeOneAsSuccess: true, ct).ConfigureAwait(false);
        var entries = output
            .SplitLines()
            .Where(static rawLine => !string.IsNullOrWhiteSpace(rawLine))
            .Select(Path.GetFullPath)
            .Where(fullPath => IsInsideRoot(root, fullPath))
            .Select(fullPath => new ReadOnlyFileListEntry(ToDisplayPath(root, fullPath)))
            .OrderBy(static entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = entries
            .Skip(effectiveOffset)
            .Take(effectiveMaxResults)
            .ToList();
        var hasMore = effectiveOffset + selected.Count < entries.Count;
        var nextOffset = hasMore ? effectiveOffset + selected.Count : (int?)null;

        return new ReadOnlyFileListResult(
            root.Name,
            ToDisplayPath(root, listPath),
            string.IsNullOrWhiteSpace(glob) ? null : glob.Trim(),
            effectiveOffset,
            effectiveMaxResults,
            selected,
            selected.Count,
            hasMore,
            hasMore,
            nextOffset,
            hasMore ? "More files may exist. Increase offset to continue from the next result window." : null);
    }

    private async Task<string> RunRipgrepAsync(
        IReadOnlyList<string> arguments,
        bool treatExitCodeOneAsSuccess,
        CancellationToken ct)
    {
        var timeoutSeconds = Math.Max(1, options.RipgrepTimeoutSeconds);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(options.RipgrepExecutablePath)
                ? "rg"
                : options.RipgrepExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start ripgrep.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode == 0 || treatExitCodeOneAsSuccess && process.ExitCode == 1)
            {
                return output;
            }

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"Ripgrep exited with code {process.ExitCode}."
                    : $"Ripgrep exited with code {process.ExitCode}: {error.Trim()}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException($"Ripgrep exceeded the configured timeout of {timeoutSeconds} seconds.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            logger.LogWarning(ex, "Failed to start ripgrep executable '{RipgrepPath}'.", startInfo.FileName);
            throw new InvalidOperationException(
                $"Ripgrep executable '{startInfo.FileName}' was not found or could not be started. Configure RipgrepExecutablePath or install rg.",
                ex);
        }
        finally
        {
            if (process.StartInfo.FileName is not null && !HasProcessExited(process))
            {
                TryKillProcess(process);
            }
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!HasProcessExited(process))
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup only. The original tool error is more useful to the agent.
        }
    }

    private static bool HasProcessExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private ReadOnlyFileRoot GetRoot(string rootName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootName);

        return _roots.FirstOrDefault(root => string.Equals(root.Name, rootName.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new KeyNotFoundException($"Read-only file access root '{rootName}' is not configured.");
    }

    private static IReadOnlyList<ReadOnlyFileRoot> BuildRoots(IReadOnlyList<ReadOnlyFileAccessRootRegistration> registrations)
    {
        var roots = new List<ReadOnlyFileRoot>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            if (!names.Add(registration.Name))
            {
                throw new InvalidOperationException($"Duplicate read-only file access root name '{registration.Name}'.");
            }

            var fullPath = ResolveRootPath(registration.Path);
            roots.Add(new ReadOnlyFileRoot(
                registration.Name,
                registration.Description,
                EnsureTrailingDirectorySeparator(fullPath)));
        }

        return roots;
    }

    private static string ResolveRootPath(string path)
    {
        var trimmed = path.Trim();
        var absolutePath = Path.IsPathRooted(trimmed)
            ? trimmed
            : RuntimePathHelper.GetRelativePathInRunningPath(trimmed);

        return Path.GetFullPath(absolutePath);
    }

    private static string ResolvePath(ReadOnlyFileRoot root, string? relativePath, bool requireFilePath)
    {
        var normalizedRelative = NormalizeRelativePath(relativePath);
        if (requireFilePath && string.IsNullOrWhiteSpace(normalizedRelative))
        {
            throw new ArgumentException("A relative file path is required.", nameof(relativePath));
        }

        var fullPath = string.IsNullOrWhiteSpace(normalizedRelative)
            ? root.FullPath
            : Path.GetFullPath(Path.Combine(root.FullPath, normalizedRelative));

        if (!IsInsideRoot(root, fullPath))
        {
            throw new UnauthorizedAccessException(
                $"Path '{relativePath}' escapes configured root '{root.Name}'. Use only relative paths inside that root.");
        }

        EnsureNoReparseTraversal(root, fullPath);
        return fullPath;
    }

    private static string NormalizeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            throw new UnauthorizedAccessException("Absolute paths are not accepted by read-only file access tools.");
        }

        return trimmed.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    private static bool IsInsideRoot(ReadOnlyFileRoot root, string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath);
        return string.Equals(
                   normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   root.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(root.FullPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNoReparseTraversal(ReadOnlyFileRoot root, string fullPath)
    {
        var rootPath = root.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.Exists(rootPath) && HasReparsePoint(rootPath))
        {
            throw new UnauthorizedAccessException(
                $"Configured root '{root.Name}' resolves through a reparse point or symbolic link, which is not allowed by the read-only file access skill.");
        }

        var relative = Path.GetRelativePath(root.FullPath, fullPath);
        if (relative == ".")
        {
            return;
        }

        var current = rootPath;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Path.Exists(current))
            {
                return;
            }

            if (HasReparsePoint(current))
            {
                throw new UnauthorizedAccessException(
                    $"Path '{relative}' resolves through a reparse point or symbolic link, which is not allowed by the read-only file access skill.");
            }
        }
    }

    private static bool HasReparsePoint(string path)
        => File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    private static string ToDisplayPath(ReadOnlyFileRoot root, string fullPath)
    {
        var relative = Path.GetRelativePath(root.FullPath, fullPath);
        return relative == "."
            ? string.Empty
            : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static async Task<bool> LooksBinaryAsync(string path, CancellationToken ct)
    {
        var buffer = new byte[4096];
        await using var stream = File.OpenRead(path);
        var bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
        return buffer.AsSpan(0, bytesRead).Contains((byte)0);
    }

    private static string BuildReadMessage(int? nextStartLine, bool tokenTruncated)
    {
        if (nextStartLine is not null && tokenTruncated)
        {
            return $"Content loaded and truncated by token budget. Continue with startLine {nextStartLine} for the next line window.";
        }

        if (nextStartLine is not null)
        {
            return $"Content loaded. Continue with startLine {nextStartLine} for the next line window.";
        }

        return tokenTruncated
            ? "Content loaded and truncated by token budget."
            : "Content loaded.";
    }

    private static RipgrepSearchParseResult ParseRipgrepSearch(
        string output,
        ReadOnlyFileRoot root,
        int offset,
        int maxResults,
        int maxLineCharacters)
    {
        var results = new List<ReadOnlyFileSearchLine>();
        var seen = 0;
        var truncated = false;

        foreach (var line in output.SplitLines())
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var rootElement = document.RootElement;
            var type = rootElement.GetProperty("type").GetString();
            if (type is not ("match" or "context"))
            {
                continue;
            }

            if (!rootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("path", out var pathElement)
                || !pathElement.TryGetProperty("text", out var pathText)
                || !data.TryGetProperty("lines", out var linesElement)
                || !linesElement.TryGetProperty("text", out var textElement)
                || !data.TryGetProperty("line_number", out var lineNumberElement))
            {
                continue;
            }

            if (seen++ < offset)
            {
                continue;
            }

            if (results.Count >= maxResults)
            {
                truncated = true;
                break;
            }

            var fullPath = Path.GetFullPath(pathText.GetString() ?? string.Empty);
            if (!IsInsideRoot(root, fullPath))
            {
                continue;
            }

            var text = TrimLineEnding(textElement.GetString() ?? string.Empty);
            var truncatedText = TruncateLine(text, maxLineCharacters);

            results.Add(new ReadOnlyFileSearchLine(
                ToDisplayPath(root, fullPath),
                lineNumberElement.GetInt32(),
                truncatedText.Text,
                string.Equals(type, "match", StringComparison.Ordinal),
                truncatedText.WasTruncated));
        }

        return new RipgrepSearchParseResult(results, truncated);
    }

    private static string TrimLineEnding(string value)
        => value.TrimEnd('\r', '\n');

    private static LineTruncation TruncateLine(string text, int maxCharacters)
    {
        var effectiveMax = Math.Max(1, maxCharacters);
        return text.Length <= effectiveMax
            ? new LineTruncation(text, false)
            : new LineTruncation(text[..effectiveMax] + "...", true);
    }

    private static int Clamp(int value, int min, int max, int fallback)
    {
        var effectiveMax = Math.Max(min, max);
        if (value <= 0)
        {
            value = fallback;
        }

        return Math.Min(effectiveMax, Math.Max(min, value));
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
               || fullPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private sealed record ReadOnlyFileRoot(
        string Name,
        string? Description,
        string FullPath);

    private sealed record RipgrepSearchParseResult(
        IReadOnlyList<ReadOnlyFileSearchLine> Results,
        bool Truncated);

    private sealed record LineTruncation(
        string Text,
        bool WasTruncated);
}

internal static class ReadOnlyFileAccessStringExtensions
{
    internal static IEnumerable<string> SplitLines(this string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
