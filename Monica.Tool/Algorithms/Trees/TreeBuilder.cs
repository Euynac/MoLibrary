namespace Monica.Tool.Algorithms.Trees;

/// <summary>
/// Factory methods for building tree structures from various data sources.
/// </summary>
public static class TreeBuilder
{
    /// <summary>
    /// Builds tree(s) from a flat collection using key/parent-key relationships.
    /// Items whose parent key is null or not found become root nodes.
    /// </summary>
    /// <returns>A list of root nodes.</returns>
    public static List<TreeNode<TData>> BuildFromFlat<TData, TKey>(
        IEnumerable<TData> items,
        Func<TData, TKey> keySelector,
        Func<TData, TKey?> parentKeySelector,
        IEqualityComparer<TKey>? comparer = null) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(parentKeySelector);

        comparer ??= EqualityComparer<TKey>.Default;
        var nodeMap = new Dictionary<TKey, TreeNode<TData>>(comparer);
        var childBuffer = new Dictionary<TKey, List<TreeNode<TData>>>(comparer);
        var roots = new List<TreeNode<TData>>();

        foreach (var item in items)
        {
            var key = keySelector(item);
            var node = new TreeNode<TData>(item);
            if (!nodeMap.TryAdd(key, node))
            {
                throw new ArgumentException($"Duplicate tree key '{key}' was found.", nameof(items));
            }

            // Attach any children that were waiting for this node
            if (childBuffer.TryGetValue(key, out var waiting))
            {
                node.AddChildren(waiting);
                childBuffer.Remove(key);
            }

            var parentKey = parentKeySelector(item);
            if (parentKey is null)
            {
                roots.Add(node);
                continue;
            }

            if (nodeMap.TryGetValue(parentKey, out var parent))
            {
                parent.AddChild(node);
            }
            else
            {
                // Parent not yet seen — buffer this child
                if (!childBuffer.TryGetValue(parentKey, out var list))
                {
                    list = [];
                    childBuffer[parentKey] = list;
                }
                list.Add(node);
            }
        }

        // Any remaining buffered children have no parent — treat as roots
        foreach (var list in childBuffer.Values)
        {
            roots.AddRange(list);
        }

        return roots;
    }

    /// <summary>
    /// Builds a tree from path strings, creating intermediate directory nodes as needed.
    /// Returns a virtual root node containing the top-level entries.
    /// </summary>
    public static TreeNode<TPathData> BuildFromPaths<TItem, TPathData>(
        IEnumerable<TItem> items,
        Func<TItem, string> pathSelector,
        Func<string, TPathData> directoryDataFactory,
        Func<TItem, string, TPathData> leafDataFactory,
        char separator = '/')
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(pathSelector);
        ArgumentNullException.ThrowIfNull(directoryDataFactory);
        ArgumentNullException.ThrowIfNull(leafDataFactory);

        var root = new TreeNode<TPathData>(directoryDataFactory(string.Empty));
        var dirCache = new Dictionary<string, TreeNode<TPathData>>();

        foreach (var item in items)
        {
            var path = pathSelector(item);
            var parts = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var parent = root;
            // Create/find intermediate directory nodes
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var dirPath = string.Join(separator, parts, 0, i + 1);
                if (!dirCache.TryGetValue(dirPath, out var dirNode))
                {
                    dirNode = parent.AddChild(directoryDataFactory(parts[i]));
                    dirCache[dirPath] = dirNode;
                }
                parent = dirNode;
            }

            // Add the leaf node
            var leafName = parts[^1];
            parent.AddChild(leafDataFactory(item, leafName));
        }

        return root;
    }
}
