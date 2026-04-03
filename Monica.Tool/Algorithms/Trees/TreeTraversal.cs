namespace Monica.Tool.Algorithms.Trees;

/// <summary>
/// Specifies the order of tree traversal.
/// </summary>
public enum TreeTraversalOrder
{
    /// <summary>
    /// Depth-first pre-order: visit node before children.
    /// </summary>
    PreOrder,

    /// <summary>
    /// Depth-first post-order: visit children before node.
    /// </summary>
    PostOrder,

    /// <summary>
    /// Breadth-first: visit nodes level by level.
    /// </summary>
    BreadthFirst
}

/// <summary>
/// Extension methods for traversing tree structures.
/// </summary>
public static class TreeTraversal
{
    /// <summary>
    /// Traverses the subtree in depth-first order.
    /// </summary>
    public static IEnumerable<TreeNode<TData>> TraverseDfs<TData>(
        this TreeNode<TData> node,
        TreeTraversalOrder order = TreeTraversalOrder.PreOrder)
    {
        return order switch
        {
            TreeTraversalOrder.PreOrder => TraverseDfsPreOrder(node),
            TreeTraversalOrder.PostOrder => TraverseDfsPostOrder(node),
            _ => throw new ArgumentOutOfRangeException(nameof(order))
        };
    }

    /// <summary>
    /// Traverses the subtree in breadth-first (level) order.
    /// </summary>
    public static IEnumerable<TreeNode<TData>> TraverseBfs<TData>(
        this TreeNode<TData> node)
    {
        var queue = new Queue<TreeNode<TData>>();
        queue.Enqueue(node);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            foreach (var child in current.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Traverses the subtree using the specified traversal order.
    /// </summary>
    public static IEnumerable<TreeNode<TData>> Traverse<TData>(
        this TreeNode<TData> node,
        TreeTraversalOrder order)
    {
        return order switch
        {
            TreeTraversalOrder.PreOrder => TraverseDfsPreOrder(node),
            TreeTraversalOrder.PostOrder => TraverseDfsPostOrder(node),
            TreeTraversalOrder.BreadthFirst => node.TraverseBfs(),
            _ => throw new ArgumentOutOfRangeException(nameof(order))
        };
    }

    /// <summary>
    /// Returns all ancestors from parent to root (excludes self).
    /// </summary>
    public static IEnumerable<TreeNode<TData>> GetAncestors<TData>(this TreeNode<TData> node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Returns the path from root to this node (inclusive).
    /// </summary>
    public static IEnumerable<TreeNode<TData>> GetPath<TData>(this TreeNode<TData> node)
    {
        var path = new Stack<TreeNode<TData>>();
        var current = node;
        while (current is not null)
        {
            path.Push(current);
            current = current.Parent;
        }

        while (path.Count > 0)
        {
            yield return path.Pop();
        }
    }

    private static IEnumerable<TreeNode<TData>> TraverseDfsPreOrder<TData>(TreeNode<TData> node)
    {
        var stack = new Stack<TreeNode<TData>>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            var children = current.Children;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }
    }

    private static IEnumerable<TreeNode<TData>> TraverseDfsPostOrder<TData>(TreeNode<TData> node)
    {
        var stack = new Stack<(TreeNode<TData> Node, bool Visited)>();
        stack.Push((node, false));

        while (stack.Count > 0)
        {
            var (current, visited) = stack.Pop();

            if (visited)
            {
                yield return current;
                continue;
            }

            stack.Push((current, true));

            var children = current.Children;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push((children[i], false));
            }
        }
    }
}
