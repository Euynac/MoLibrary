using Microsoft.Extensions.Options;
using Monica.AI.KnowledgeBase.Abstractions;
using Monica.Modules;
using Monica.Tool.Runtime;

namespace Monica.AI.KnowledgeBase.Services;

/// <summary>
/// File-backed document source store.
/// </summary>
public sealed class FileKnowledgeDocumentSourceStore(IOptions<ModuleKnowledgeBaseOption> options)
    : IKnowledgeDocumentSourceStore
{
    private readonly string _rootPath =
        RuntimePathHelper.GetRelativePathInRunningPath(options.Value.UploadedDocumentSourceRootPath);

    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SaveContentAsync(
        string knowledgeBaseId,
        string documentPath,
        string content,
        CancellationToken ct = default)
    {
        var fullPath = GetContentFilePath(knowledgeBaseId, documentPath);

        await _lock.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetContentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        var fullPath = GetContentFilePath(knowledgeBaseId, documentPath);

        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(fullPath))
            {
                return null;
            }

            return await File.ReadAllTextAsync(fullPath, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteContentAsync(
        string knowledgeBaseId,
        string documentPath,
        CancellationToken ct = default)
    {
        var fullPath = GetContentFilePath(knowledgeBaseId, documentPath);

        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(fullPath))
            {
                return;
            }

            File.Delete(fullPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteKnowledgeBaseAsync(string knowledgeBaseId, CancellationToken ct = default)
    {
        var kbRoot = Path.Combine(_rootPath, SanitizeSegment(knowledgeBaseId));

        await _lock.WaitAsync(ct);
        try
        {
            if (!Directory.Exists(kbRoot))
            {
                return;
            }

            Directory.Delete(kbRoot, recursive: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetContentFilePath(string knowledgeBaseId, string documentPath)
    {
        var sanitizedKnowledgeBaseId = SanitizeSegment(knowledgeBaseId);
        var normalizedPath = (documentPath ?? string.Empty).Replace('\\', '/').Trim('/');

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => segment is not "." and not "..")
            .Select(SanitizeSegment)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();

        if (segments.Count == 0)
        {
            segments.Add("document");
        }

        var fileName = segments[^1];
        if (Path.GetExtension(fileName).Length == 0)
        {
            segments[^1] = $"{fileName}.txt";
        }

        var relativePath = Path.Combine(segments.ToArray());
        return Path.Combine(_rootPath, sanitizedKnowledgeBaseId, relativePath);
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = value.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(buffer).Replace(':', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "_" : sanitized;
    }
}
