namespace Monica.Tool.Algorithms.Trees;

/// <summary>
/// Extension methods for querying tree structures.
/// </summary>
public static class TreeQuery
{
    /// <summary>
    /// Finds the first node in the subtree whose data matches the predicate (DFS).
    /// </summary>
    public static TreeNode<TData>? Find<TData>(
        this TreeNode<TData> node, Func<TData, bool> predicate)
    {
        foreach (var n in node)
        {
            if (predicate(n.Data)) return n;
        }
        return null;
    }

    /// <summary>
    /// Finds all nodes in the subtree whose data matches the predicate.
    /// </summary>
    public static IEnumerable<TreeNode<TData>> FindAll<TData>(
        this TreeNode<TData> node, Func<TData, bool> predicate)
    {
        foreach (var n in node)
        {
            if (predicate(n.Data)) yield return n;
        }
    }

    /// <summary>Returns all leaf nodes in the subtree.</summary>
    public static IEnumerable<TreeNode<TData>> GetLeaves<TData>(this TreeNode<TData> node)
    {
        foreach (var n in node) { if (n.IsLeaf) yield return n; }
    }

    /// <summary>Returns all non-leaf (branch) nodes in the subtree.</summary>
    public static IEnumerable<TreeNode<TData>> GetBranches<TData>(this TreeNode<TData> node)
    {
        foreach (var n in node) { if (!n.IsLeaf) yield return n; }
    }

    /// <summary>Returns all descendant nodes (excludes self).</summary>
    public static IEnumerable<TreeNode<TData>> GetDescendants<TData>(this TreeNode<TData> node)
    {
        foreach (var n in node) { if (!ReferenceEquals(n, node)) yield return n; }
    }

    /// <summary>Returns all data values in the subtree in DFS pre-order.</summary>
    public static IEnumerable<TData> FlattenData<TData>(this TreeNode<TData> node)
    {
        foreach (var n in node) { yield return n.Data; }
    }

    /// <summary>Returns all nodes at the specified depth relative to this node.</summary>
    public static IEnumerable<TreeNode<TData>> GetNodesAtDepth<TData>(
        this TreeNode<TData> node, int depth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(depth);
        if (depth == 0) { yield return node; yield break; }

        var queue = new Queue<(TreeNode<TData> Node, int Level)>();
        queue.Enqueue((node, 0));
        while (queue.Count > 0)
        {
            var (current, level) = queue.Dequeue();
            if (level == depth) { yield return current; continue; }
            if (level >= depth) continue;
            foreach (var child in current.Children)
                queue.Enqueue((child, level + 1));
        }
    }
}
