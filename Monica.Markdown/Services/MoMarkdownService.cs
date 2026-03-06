using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Markdown.Modules;
using Monica.Tool.Algorithm.Tree;

namespace Monica.Markdown.Services;

/// <summary>
/// Singleton service for managing markdown document groups.
/// Uses lazy async initialization with SemaphoreSlim for thread safety.
/// Delegates scanning and content retrieval to <see cref="IMarkdownDocumentProvider"/>.
/// </summary>
public class MoMarkdownService(
    IOptions<ModuleMarkdownOption> options,
    IMarkdownDocumentProvider documentProvider,
    ILogger<MoMarkdownService> logger) : IMoMarkdownService, IDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly IMarkdownChangeNotifier? _changeNotifier = documentProvider.GetChangeNotifier();
    private readonly Dictionary<string, CancellationTokenSource> _pendingRefreshTokens = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly Lock _watcherLock = new();
    private volatile bool _initialized;
    private int _changeNotifierInitialized;

    private Dictionary<string, MarkdownDocumentGroup> _groups = new(
        StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, MarkdownDocument> _pathIndex = new(
        StringComparer.OrdinalIgnoreCase);

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await ScanAllGroupsAsync();
            EnsureChangeNotifierInitialized();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task ScanAllGroupsAsync()
    {
        var opt = options.Value;
        var newGroups = new Dictionary<string, MarkdownDocumentGroup>(
            StringComparer.OrdinalIgnoreCase);
        var newPathIndex = new Dictionary<string, MarkdownDocument>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var reg in opt.DocumentGroupRegistrations)
        {
            logger.LogDebug("Scanning document group '{Key}' at '{BasePath}'",
                reg.Key, reg.BasePath);

            var group = await documentProvider.ScanGroupAsync(reg, opt);
            newGroups[group.Key] = group;

            if (!group.IsValid)
            {
                logger.LogWarning(
                    "Document group '{Key}' base path does not exist: {BasePath}",
                    group.Key, group.BasePath);
                continue;
            }

            // Build flat path index from tree leaves
            foreach (var node in group.RootNode.GetLeaves())
            {
                if (node.Data is { IsDocument: true, Document: not null })
                {
                    newPathIndex[node.Data.Document.FilePath] = node.Data.Document;
                }
            }

            logger.LogInformation(
                "Document group '{Key}' scanned: {Count} documents found",
                group.Key, group.DocumentCount);
        }

        _groups = newGroups;
        _pathIndex = newPathIndex;

        UpdateAllWatchers();
    }

    public async Task<List<MarkdownDocumentGroup>> GetAllDocumentGroupsAsync()
    {
        await EnsureInitializedAsync();
        return _groups.Values.ToList();
    }

    public async Task<MarkdownDocumentGroup> GetDocumentGroupAsync(string groupKey)
    {
        await EnsureInitializedAsync();
        if (!_groups.TryGetValue(groupKey, out var group))
            throw new KeyNotFoundException(
                $"Document group '{groupKey}' not found.");
        return group;
    }

    public async Task<List<MarkdownDocument>> GetDocumentsAsync(string groupKey)
    {
        await EnsureInitializedAsync();
        if (!_groups.TryGetValue(groupKey, out var group))
            throw new KeyNotFoundException(
                $"Document group '{groupKey}' not found.");

        return group.RootNode.GetLeaves()
            .Where(n => n.Data is { IsDocument: true, Document: not null })
            .Select(n => n.Data.Document!)
            .ToList();
    }

    public async Task<TreeNode<MarkdownDocumentNodeData>>
        GetDocumentTreeAsync(string groupKey)
    {
        await EnsureInitializedAsync();
        if (!_groups.TryGetValue(groupKey, out var group))
            throw new KeyNotFoundException(
                $"Document group '{groupKey}' not found.");
        return group.RootNode;
    }

    public async Task<MarkdownDocument> GetDocumentByPathAsync(string filePath)
    {
        await EnsureInitializedAsync();

        // Try absolute path first
        var absolutePath = Path.GetFullPath(filePath);
        if (_pathIndex.TryGetValue(absolutePath, out var doc))
            return doc;

        // Try matching by relative path across all groups
        foreach (var group in _groups.Values)
        {
            var candidate = group.RootNode.GetLeaves()
                .FirstOrDefault(n =>
                    n.Data is { IsDocument: true, Document: not null }
                    && string.Equals(n.Data.Document.RelativePath, filePath,
                        StringComparison.OrdinalIgnoreCase));

            if (candidate?.Data.Document is not null)
                return candidate.Data.Document;
        }

        throw new KeyNotFoundException(
            $"Document not found for path '{filePath}'.");
    }

    public async Task<string> GetDocumentContentAsync(MarkdownDocument doc)
    {
        return await GetDocumentContentByPathAsync(doc.FilePath);
    }

    public async Task<string> GetDocumentContentByPathAsync(string filePath)
    {
        return await documentProvider.GetDocumentContentAsync(filePath);
    }

    public async Task<List<MarkdownDocument>> SearchByMetadataAsync(
        string key, string value, IEnumerable<string>? groupKeys = null)
    {
        await EnsureInitializedAsync();
        var targetGroups = ResolveTargetGroups(groupKeys);
        var results = new List<MarkdownDocument>();

        foreach (var group in targetGroups)
        {
            foreach (var node in group.RootNode.GetLeaves())
            {
                if (node.Data is not { IsDocument: true, Document.FrontMatter: not null })
                    continue;

                var doc = node.Data.Document!;
                if (MatchesMetadata(doc.FrontMatter!, key, value))
                    results.Add(doc);
            }
        }

        return results;
    }

    public async Task<List<MarkdownDocument>> SearchByMetadataAsync(
        Dictionary<string, string> criteria, IEnumerable<string>? groupKeys = null)
    {
        await EnsureInitializedAsync();
        var targetGroups = ResolveTargetGroups(groupKeys);
        var results = new List<MarkdownDocument>();

        foreach (var group in targetGroups)
        {
            foreach (var node in group.RootNode.GetLeaves())
            {
                if (node.Data is not { IsDocument: true, Document.FrontMatter: not null })
                    continue;

                var doc = node.Data.Document!;
                var allMatch = criteria.All(kvp =>
                    MatchesMetadata(doc.FrontMatter!, kvp.Key, kvp.Value));

                if (allMatch)
                    results.Add(doc);
            }
        }

        return results;
    }

    public async Task RefreshAllAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            await ScanAllGroupsAsync();
            logger.LogInformation("All document groups refreshed.");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task RefreshAsync(string groupKey)
    {
        await EnsureInitializedAsync();
        var opt = options.Value;
        var reg = opt.DocumentGroupRegistrations
            .FirstOrDefault(r => string.Equals(
                r.Key, groupKey, StringComparison.OrdinalIgnoreCase));

        if (reg is null)
            throw new KeyNotFoundException(
                $"Document group '{groupKey}' is not registered.");

        await _refreshLock.WaitAsync();
        try
        {
            var group = await documentProvider.ScanGroupAsync(reg, opt);
            _groups[group.Key] = group;

            // Rebuild path index
            RebuildPathIndex();
            UpdateWatcher(group);

            logger.LogInformation(
                "Document group '{Key}' refreshed: {Count} documents",
                group.Key, group.DocumentCount);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private List<MarkdownDocumentGroup> ResolveTargetGroups(
        IEnumerable<string>? groupKeys)
    {
        if (groupKeys is null)
            return _groups.Values.ToList();

        var keys = groupKeys.ToList();
        return keys.Count == 0
            ? _groups.Values.ToList()
            : _groups.Values
                .Where(g => keys.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
                .ToList();
    }

    private static bool MatchesMetadata(
        MarkdownFrontMatter frontMatter, string key, string value)
    {
        if (!frontMatter.RawMetadata.TryGetValue(key, out var rawValue)
            || rawValue is null)
            return false;

        // List value: contains check (e.g., tags)
        if (rawValue is List<object?> list)
        {
            return list.Any(item =>
                item is not null
                && string.Equals(item.ToString(), value,
                    StringComparison.OrdinalIgnoreCase));
        }

        // String or other: equality check
        return string.Equals(
            rawValue.ToString(), value, StringComparison.OrdinalIgnoreCase);
    }

    private void RebuildPathIndex()
    {
        var newIndex = new Dictionary<string, MarkdownDocument>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in _groups.Values)
        {
            foreach (var node in group.RootNode.GetLeaves())
            {
                if (node.Data is { IsDocument: true, Document: not null })
                    newIndex[node.Data.Document.FilePath] = node.Data.Document;
            }
        }

        _pathIndex = newIndex;
    }

    private void EnsureChangeNotifierInitialized()
    {
        if (_changeNotifier is null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _changeNotifierInitialized, 1) == 1)
        {
            return;
        }

        _changeNotifier.DocumentsChanged += OnDocumentsChanged;
    }

    private void UpdateAllWatchers()
    {
        if (_changeNotifier is null)
        {
            return;
        }

        foreach (var group in _groups.Values)
        {
            UpdateWatcher(group);
        }
    }

    private void UpdateWatcher(MarkdownDocumentGroup group)
    {
        if (_changeNotifier is null)
        {
            return;
        }

        _changeNotifier.StopWatching(group.Key);
        if (group.IsValid)
        {
            _changeNotifier.StartWatching(group.Key, group.BasePath);
        }
    }

    private void OnDocumentsChanged(object? sender, DocumentsChangedEventArgs e)
    {
        if (_changeNotifier is null)
        {
            return;
        }

        CancellationTokenSource refreshCts;
        lock (_watcherLock)
        {
            if (_pendingRefreshTokens.TryGetValue(e.GroupKey, out var existing))
            {
                existing.Cancel();
                existing.Dispose();
            }

            refreshCts = new CancellationTokenSource();
            _pendingRefreshTokens[e.GroupKey] = refreshCts;
        }

        _ = DebouncedRefreshAsync(e.GroupKey, refreshCts.Token);
    }

    private async Task DebouncedRefreshAsync(string groupKey, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            await RefreshAsync(groupKey);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh document group '{GroupKey}' from change notification", groupKey);
        }
        finally
        {
            lock (_watcherLock)
            {
                if (_pendingRefreshTokens.TryGetValue(groupKey, out var existing)
                    && existing.Token == cancellationToken)
                {
                    existing.Dispose();
                    _pendingRefreshTokens.Remove(groupKey);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_changeNotifier is not null)
        {
            _changeNotifier.DocumentsChanged -= OnDocumentsChanged;
            _changeNotifier.Dispose();
        }

        lock (_watcherLock)
        {
            foreach (var pending in _pendingRefreshTokens.Values)
            {
                pending.Cancel();
                pending.Dispose();
            }

            _pendingRefreshTokens.Clear();
        }

        _initLock.Dispose();
        _refreshLock.Dispose();
    }
}
